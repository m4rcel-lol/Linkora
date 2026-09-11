# Linkora

A Windows Live Messenger style instant messenger, running on **macOS**, **Linux** and **Windows**.

![Preview1](./Media/wlmclient1.png)

## Features
+ Add / Remove / Block Contact
+ Favourite contacts
+ Send Nudge
+ File attachments in chat
+ Avatars / profile pictures shown in the contact list
+ Interface in your own language
+ Light and dark themes
+ Voice calls
+ Quick Message
+ Emoticons
+ Web Registration
+ Encryption
+ Save ID & Password
+ Auto Login
+ Notification Bubble
+ Automatic Away Status (AFK 150 Seconds)

## Project layout

| Project      | What it is                                                                       |
| ------------ | -------------------------------------------------------------------------------- |
| `WLMClient`  | The Linkora client UI (Avalonia).                                                     |
| `WLMServer`  | The console server. Talks to MySQL and relays messages between clients.          |
| `WLMData`    | Packet definitions shared by both.                                               |
| `WLMNet`     | The TCP transport: framing, AES encryption and packet dispatch.                  |

---

# Quick start

You need three things running: a **MySQL database**, the **server**, and one or more **clients**.

## 1. Database

Create a database called `msn` and import the schema:

```bash
mysql -u root -p -e "CREATE DATABASE msn CHARACTER SET utf8;"
mysql -u root -p msn < Database/msn.sql
```

That creates the two tables the server uses: `account` and `friend_requests`.

It is worth creating a dedicated user rather than using `root`:

```bash
mysql -u root -p -e "CREATE USER 'wlm'@'localhost' IDENTIFIED BY 'a-strong-password'; GRANT ALL ON msn.* TO 'wlm'@'localhost'; FLUSH PRIVILEGES;"
```

## 2. Server

Open `Messenger.config` next to the `WLMServer` executable and fill in your details:

```xml
<appSettings>
  <add key="server_port" value="1323" />
  <add key="server_encryption_key" value="CHANGEME" />
  <add key="database_host" value="localhost" />
  <add key="database_id" value="root" />
  <add key="database_password" value="" />
  <add key="database_password_encryption_key" value="CHANGE_ME_123456_123456_" />
  <add key="database_password_encryption_iv" value="CHANGE_ME_123456" />
  <add key="avatars_enabled" value="false" />
  <add key="avatars_address" value="" />
  <add key="avatars_address_upload" value="" />
  <add key="avatars_http_port" value="0" />
  <add key="avatars_storage_path" value="uploads" />
  <add key="broadcast_interval" value="30" />
</appSettings>
```

| Setting                             | Description                                                                                                                 |
| ----------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| server_port                         | *What port to use.*                                                                                                         |
| server_encryption_key               | *Encryption key for talking to the client. Must match the client's value — see "Matching the encryption key" below.*        |
| database_host                       | *Database host holding the account information.*                                                                            |
| database_id                         | *Database user.*                                                                                                            |
| database_password                   | *Database user password.*                                                                                                   |
| database_password_encryption_key    | *Key used to encrypt stored account passwords. Change the value but **keep the length** (24 characters).*                   |
| database_password_encryption_iv     | *IV used for account password encryption. Change the value but **keep the length** (16 characters).*                        |
| avatars_enabled                     | *Whether to use avatars (true/false). Requires the registration page below.*                                                |
| avatars_address                     | *Where avatars are served from, e.g. `http://localhost/uploads/`*                                                           |
| avatars_address_upload              | *The upload endpoint the client posts to, e.g. `http://localhost/upload.php`*                                               |
| avatars_http_port                   | *Port for the built-in avatar webserver. `0` disables it and expects an external webserver instead.*                        |
| avatars_storage_path                | *Where the built-in webserver keeps uploaded pictures. Relative paths are resolved next to the executable.*                 |
| broadcast_interval                  | *How often (seconds) the server refreshes every connected user's contact list.*                                             |

> **Important:** `database_password_encryption_key` must be exactly 24 characters and
> `database_password_encryption_iv` exactly 16. These derive the AES key used for stored
> passwords — changing them later invalidates every existing account password.

Start it:

```bash
./WLMServer
```

You should see `Local End Point: 0.0.0.0:1323`. Server commands:

| Command       | Description                       |
| ------------- | --------------------------------- |
| `/help`       | Show the command list             |
| `/online`     | Number of users currently online  |
| `/listonline` | List online users and their IPs   |
| `/create`     | Register a new account            |

Create your first accounts with `/create` (it prompts for a username and password), or let
people register themselves through the web page described below.

## 3. Client

The sign in page has a **server address** box: type the address of whichever Linkora server you
want to use (`127.0.0.1`, `chat.example.com`, or `chat.example.com:1323` to override the port).
It is remembered between sessions, so most people only ever set it once.

`Messenger.config` next to the client supplies the value the box starts out with:

```xml
<appSettings>
  <add key="server_address" value="127.0.0.1" />
  <add key="server_port" value="1323" />
  <add key="registration_url" value="" />
</appSettings>
```

| Setting            | Description                                                                                       |
| ------------------ | ------------------------------------------------------------------------------------------------- |
| server_address     | *Hostname or IP of your server.*                                                                  |
| server_port        | *Must match the server's `server_port`.*                                                          |
| registration_url   | *Optional. Overrides where the "Sign up." link goes. Leave empty and it uses `http://<server address>/`, taken from the box on the sign in page.* |

On macOS that file lives inside the bundle, at
`Linkora.app/Contents/MacOS/Messenger.config`.

Then start the client and sign in.

### Matching the encryption key

All traffic between client and server is AES encrypted with a pre-shared key. The server reads
it from `server_encryption_key` in its config; **the client's copy is compiled in**, in
`WLMClient/Config/Properties.cs`:

```csharp
public static string SERVER_ENCRYPTION_KEY = "CHANGEME";
```

If you change the server's key you must change this constant and rebuild the client, otherwise
the two sides cannot decrypt each other and sign in will silently fail.

---

# Building

### Prerequisites
* [.NET SDK 8.0 or newer](https://dotnet.microsoft.com/download)
* A MySQL or MariaDB server

### Build and run from source

```bash
dotnet build                                    # everything
dotnet run --project WLMServer                  # the server
dotnet run --project WLMClient                  # the client
```

### Producing releases

```bash
./build-release.sh                   # every target below
./build-release.sh linux-x64         # a single target
./build-release.sh portable          # just the portable server
```

Output lands in `dist/<target>/`, with macOS builds packaged as a `.app` bundle.

| Target                                      | What you get                                                      |
| ------------------------------------------- | ------------------------------------------------------------------ |
| `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64` | Self-contained client and server. No .NET needed, ~180 MB each. |
| `portable`                                  | **One server build for every platform.** ~13 MB, needs the .NET 8 runtime installed. |

### One server for macOS and Linux

There is no such thing as a single *native* executable for two operating systems — macOS uses
Mach-O binaries and Linux uses ELF, which is why the self-contained builds are per-platform.

What you can have is one **portable** build. `dist/portable/server` holds the same files for every
platform; install the [.NET 8 runtime](https://dotnet.microsoft.com/download) and run:

```bash
dotnet WLMServer.dll
```

Worth knowing either way: **a single running server already serves macOS, Linux and Windows
clients at the same time.** They all speak the same protocol, so you never need more than one
server no matter what your users run.

### Running the macOS build

The app is not code signed, so Gatekeeper blocks it on first launch. Clear the quarantine flag:

```bash
xattr -dr com.apple.quarantine "Linkora.app"
```

### Running the Linux build

The **server** needs OpenSSL, which the MySQL connector uses for TLS. Minimal images such as
`debian:12` do not ship it, and without it the server exits with
*"No usable version of libssl was found"*:

```bash
sudo apt install libssl3          # Debian/Ubuntu
sudo dnf install openssl-libs     # Fedora
```

The **client** additionally needs a desktop session, the usual X11/Wayland client libraries and
at least one font:

```bash
sudo apt install libx11-6 libice6 libsm6 libfontconfig1 fonts-dejavu-core    # Debian/Ubuntu
sudo dnf install libX11 libICE libSM fontconfig dejavu-sans-fonts            # Fedora
```

Notification sounds are played through `paplay`, `aplay`, `pw-play` or `ffplay`, whichever is
installed. Without one of those the app runs fine, just silently.

---

# Profile pictures

Profile pictures are off by default. There are two ways to turn them on.

### Option A — the built-in avatar server (no webserver needed)

The server can host pictures itself. Set:

```xml
<add key="avatars_enabled" value="true" />
<add key="avatars_http_port" value="8080" />
<add key="avatars_address" value="http://your-server:8080/uploads/" />
<add key="avatars_address_upload" value="http://your-server:8080/upload" />
<add key="avatars_storage_path" value="uploads" />
```

On start the server reports `Avatar HTTP server listening on port 8080`. Users then set a picture
from **Options → Browse** in the client; it is resized to 100×100, uploaded, and shown to their
contacts.

`avatars_address` must be reachable *by the clients*, so use the server's real hostname or IP
rather than `localhost` when they run on other machines. Open the port in your firewall too.

### Option B — the PHP registration page

If you already run a webserver, point the two addresses at the bundled `upload.php` instead and
leave `avatars_http_port` at `0`. See the next section.

> Uploaded pictures are served over plain HTTP and are readable by anyone who can reach the port.
> Put it behind a reverse proxy with TLS if that matters to you.

---

# The contact list

Each contact shows their profile picture inside the same frame the application draws around your
own picture, which carries their status: green for available, red for busy, orange for away and
silver for offline. Contacts without a picture get the default one from the sign in page.

Underneath their name is either their personal message, or, when they have not set one, the most
recent message exchanged with them. That preview covers the current session only, since the
application keeps no message history.

Right click a contact and choose **Add to Favourites** to pin them. Favourites are listed in
their own group above everyone else, and both groups can be collapsed by clicking the heading.
The list is kept per account in `Favourites.xml`, next to the saved sign in details, so two
people sharing an installation keep separate lists.

---

# Changing your sign in name

**Options** lets you change both names an account has:

* **Display name** is what your contacts see. It changes immediately.
* **Sign in name** is what you type to sign in and what people add you by. Changing it asks the
  server, which renames the account and every reference to it — the contact list of everyone who
  has you, and any outstanding friend requests — in a single database transaction, so a failure
  part way through cannot leave people pointing at a name that no longer exists.

A sign in name can be up to 29 characters of letters, digits, dots, dashes and underscores. The
limited set is deliberate: contact lists are stored as `[name,blocked,accepted]` text, so a name
containing a bracket or a comma would corrupt every list referring to it. If the name is already
taken the server refuses and nothing changes.

Saved sign in details are updated to the new name, so "Remember me" keeps working.

---

# Dark theme

**Options → Dark theme** switches between the light and dark looks. It applies immediately to
every open window and is remembered.

The photographic header artwork is a fixed bitmap rather than something that can be recoloured,
so in the dark theme it is toned down with an overlay instead. The avatar frames and the
notification popup keep their own colours in both themes, since those carry the application's
identity.

---

# Languages

The interface language is chosen in **Options → Language**. It applies immediately and is
remembered. Out of the box Linkora speaks English, Spanish, German, French and Polish, and on
first run it follows the language the operating system is set to.

The version in use is shown at the foot of the window. Release builds add the date they were
produced, for example `Linkora 1.1.0 (build 20260911)`.

### Adding your own language

Translations are plain text files in the `Languages` folder next to the client, so adding one
needs no rebuild:

1. Copy an existing file, for example `Languages/es.lang`, to your language's code:
   `Languages/pt-BR.lang`. The file name is what the application matches against the system
   language.
2. Translate the values after each `=`. Set `language.name` to the language's own name, which is
   what the Options list shows.
3. Restart the client. The new language appears in the list.

```
language.name = Português
login.button = Entrar
main.friends = Amigos ({0}/{1})
```

Placeholders such as `{0}` are filled in by the application and must be kept. `
` starts a new
line. Lines beginning with `#` are comments. Any key you leave out falls back to English, so a
partial translation is perfectly usable.

---

# Voice calls

The phone button in a conversation places a call. The other side rings, and once they answer you
can talk. The call window shows who you are speaking to, how long for, and three controls: answer,
mute and hang up.

Audio is captured and played through OpenAL, which ships with the client, so nothing has to be
installed on the machine. It is 16 kHz mono encoded as G.711 mu-law, about 128 kbit/s each way,
carried over the same encrypted connection as everything else. The server only passes audio
between two people who are actually on a call together.

The server also tracks who is on a call, so a second caller is told the line is busy rather than
making someone's client ring twice, and calling somebody who is not signed in says so. If a client
disappears mid call the other side is told the call ended rather than being left on a call that
cannot finish.

On macOS the first call asks for microphone permission. If no microphone or output is found the
call still connects and says which one is missing.

**Not included.** Camera and screen sharing. Those need video capture and encoding, which is a
much larger piece of work than audio, so the call window does not pretend to offer them.

### The ringtone

`WLMClient/Content/WAV/ring.wav` is what plays for an incoming call, and it repeats until the call
is answered, declined or given up on. Replace that file to change it. Keep it as a WAV: the
platform players that ship with macOS and Linux do not all read Ogg or MP3.

---

# File attachments

Click the paperclip in a conversation to send files. They travel over the same encrypted
connection as messages — the server relays them without writing anything to disk, so no webserver
or extra storage is involved.

* Images are shown inline in the conversation; anything else appears as a clickable link.
* Received files are saved to **`~/Downloads/Linkora`** and open in the desktop's default
  application when clicked.
* The limit is **20 MB** per file.

---

# Registration Page (optional)

A small PHP page that lets people register accounts themselves and upload avatars.

Copy the `Registration Page` folder to your webserver and make `uploads/` writable if you intend
to use avatars.

The **"Sign up."** link on the sign in page follows whatever server address the user has typed:
with `chat.example.com` in the box it opens `http://chat.example.com/`. So if you serve this
folder from the web root of the machine running the server, the link works with no configuration
at all. Set `registration_url` in the client's `Messenger.config` only when the page lives
somewhere else, such as a different host, a subdirectory or HTTPS. Open `config.php` and fill in the database details:

```php
<?php
$dbhost = 'localhost';
$dbuser = 'root';
$dbpass = '';
$dbname = 'msn';
$encryption_key = 'CHANGE_ME_123456_123456_';
$encryption_iv = 'CHANGE_ME_123456';
?>
```

`$encryption_key` and `$encryption_iv` **must match** `database_password_encryption_key` and
`database_password_encryption_iv` in the server's `Messenger.config`, otherwise accounts created
through the web page cannot sign in.

To enable avatars, set these in the server config:

```xml
<add key="avatars_enabled" value="true" />
<add key="avatars_address" value="http://your-server/uploads/" />
<add key="avatars_address_upload" value="http://your-server/upload.php" />
```

---

# Running the server in the background

### Linux (systemd)

`/etc/systemd/system/wlmserver.service`:

```ini
[Unit]
Description=Linkora server
After=network.target mysql.service

[Service]
Type=simple
User=wlm
WorkingDirectory=/opt/wlmserver
ExecStart=/opt/wlmserver/WLMServer
Restart=on-failure

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable --now wlmserver
journalctl -u wlmserver -f
```

The server detects that no console is attached and keeps running; the interactive commands are
unavailable in that mode, so create accounts with `/create` beforehand or via the web page.

### macOS (launchd)

`~/Library/LaunchAgents/net.linkora.server.plist`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key><string>net.linkora.server</string>
    <key>ProgramArguments</key>
    <array><string>/Users/you/wlmserver/WLMServer</string></array>
    <key>WorkingDirectory</key><string>/Users/you/wlmserver</string>
    <key>RunAtLoad</key><true/>
    <key>KeepAlive</key><true/>
</dict>
</plist>
```

```bash
launchctl load ~/Library/LaunchAgents/net.linkora.server.plist
```

### Firewall

Allow inbound TCP on your `server_port` (1323 by default) if clients connect from other machines.

---

# Troubleshooting

**"Unable to connect to any of the specified MySQL hosts"**
The database is unreachable or the credentials are wrong. Check `database_host`, `database_id`
and `database_password`, and that MySQL accepts connections from the server's address.

**Sign in does nothing / client immediately drops the connection**
Almost always a mismatch between the server's `server_encryption_key` and the client's compiled
`SERVER_ENCRYPTION_KEY`. See "Matching the encryption key".

**"This version of Linkora is outdated"**
The client's `Client.version` and the server's `Server.version` strings differ. Both are `1.0.0`
by default.

**Avatars do not appear**
`avatars_enabled` must be `true` and `avatars_address` must be a URL the *client* can reach —
not `localhost` if the client runs on another machine.

**The client starts but the window is blank on Linux**
Usually a missing font package. Install `fontconfig` and a font family such as
`fonts-dejavu-core`.

**"No usable version of libssl was found" when starting the server on Linux**
Install OpenSSL (`libssl3` on Debian/Ubuntu, `openssl-libs` on Fedora). The MySQL connector
needs it to negotiate TLS with the database.

---

# Notes on the cross-platform port

The original was a .NET Framework 4.5 app built on WPF, Windows Forms and the discontinued
NetworkComms.Net library — none of which run outside Windows. The port keeps the original
layout, artwork and behaviour while replacing the Windows-only foundations:

* **UI** — WPF became [Avalonia](https://avaloniaui.net/). The XAML, styling and every control
  were carried across as-is. `WLMClient/Compat/` holds small stand-ins for the WPF pieces
  Avalonia has no equivalent of, most notably a `RichTextBox` with a flow document, caret,
  selection and inline images, which the chat window and contact list are built on.
* **Dialogs** — the three Windows Forms dialogs were rebuilt as Avalonia windows at the same
  pixel coordinates, fonts and colours as the designer files specified.
* **Networking** — NetworkComms.Net was replaced by `WLMNet`, which keeps the same API surface
  the application was written against (so the packet handlers are unchanged) over a simple
  length-prefixed TCP framing with AES-256-CBC encryption.
* **Serialisation** — `BinaryFormatter` is gone from .NET; packets now serialise through
  protobuf-net, which the packet classes were already annotated for.
* **Platform services** — idle detection for the automatic away status, the window attention
  request behind Nudge, and sound playback each have per-platform implementations in
  `WLMClient/Compat/Platform.cs`.
* **Database** — the MySQL connection string no longer requests `Keepalive`, which the connector
  implements with a Windows-only socket call that throws elsewhere.
