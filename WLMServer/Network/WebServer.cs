using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

using WLMServer.Database;

namespace WLMServer.Network
{
    /// <summary>
    /// The server's own small website, so a Linkora server needs nothing else installed to be
    /// usable. It serves:
    ///
    ///   GET  /                the sign up page
    ///   GET  /signup          the same page
    ///   POST /signup          creates the account
    ///   POST /upload          multipart/form-data with a "file" field, replies with the stored name
    ///   GET  /uploads/&lt;name&gt;  serves a stored picture
    ///
    /// The avatar endpoints are the two the client already expected from the bundled upload.php,
    /// so pointing avatars_address and avatars_address_upload here is enough; keeping upload.php
    /// on a separate webserver still works too.
    /// </summary>
    class WebServer
    {
        /// <summary>Matches the limit the bundled upload.php enforced.</summary>
        private const int MaximumUploadSize = 5 * 1024 * 1024;

        private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".jpe", ".jfif", ".gif" };

        /// <summary>Long enough for a passphrase, short enough that nothing is being smuggled.</summary>
        private const int MaximumFormSize = 8 * 1024;

        private readonly HttpListener listener = new HttpListener();
        private readonly string uploadsDirectory;
        private readonly int port;
        private readonly bool avatarsEnabled;
        private readonly bool registrationEnabled;

        /// <summary>
        /// Registration gets its own database connection. The one the messenger side uses is a
        /// single unpooled connection driven from the network threads, and requests here arrive on
        /// thread pool threads; sharing it would mean two threads on one connection.
        /// </summary>
        private readonly object registrationLocker = new object();
        private AccountManager registrationAccounts;

        public WebServer(int port, string uploadsDirectory, bool avatarsEnabled, bool registrationEnabled)
        {
            this.port = port;
            this.uploadsDirectory = uploadsDirectory;
            this.avatarsEnabled = avatarsEnabled;
            this.registrationEnabled = registrationEnabled;

            if (avatarsEnabled)
            {
                Directory.CreateDirectory(uploadsDirectory);
            }

            listener.Prefixes.Add("http://+:" + port + "/");
        }

        public void Start()
        {
            try
            {
                listener.Start();
            }
            catch (HttpListenerException)
            {
                // Binding to every interface needs elevation on some systems; fall back to local.
                listener.Prefixes.Clear();
                listener.Prefixes.Add("http://localhost:" + port + "/");
                listener.Start();
            }

            Program.WriteToConsole("Website listening on port " + port);

            if (registrationEnabled)
            {
                Program.WriteToConsole("  sign up page at " + GetRegistrationUrl());
            }

            if (avatarsEnabled)
            {
                Program.WriteToConsole("  avatars stored in " + uploadsDirectory);
            }

            Thread thread = new Thread(Listen);
            thread.IsBackground = true;
            thread.Name = "Website";
            thread.Start();
        }

        public void Stop()
        {
            try { listener.Stop(); } catch { }
        }

        private void Listen()
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = listener.GetContext();
                }
                catch
                {
                    return;
                }

                ThreadPool.QueueUserWorkItem(state => Handle((HttpListenerContext)state), context);
            }
        }

        private void Handle(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url.AbsolutePath;
                string method = context.Request.HttpMethod;
                string trimmed = path.TrimEnd('/');

                if (avatarsEnabled && method == "POST" && trimmed.EndsWith("/upload"))
                {
                    HandleUpload(context);
                }
                else if (avatarsEnabled && method == "GET" && path.StartsWith("/uploads/"))
                {
                    HandleDownload(context, path.Substring("/uploads/".Length));
                }
                else if (registrationEnabled && method == "GET" && (trimmed.Length == 0 || trimmed == "/signup"))
                {
                    WriteHtml(context, 200, SignUpPage.Form(null, null));
                }
                else if (registrationEnabled && method == "POST" && trimmed == "/signup")
                {
                    HandleSignUp(context);
                }
                else
                {
                    WriteText(context, 404, "0");
                }
            }
            catch (Exception exception)
            {
                Program.WriteToConsole("Website error: " + exception.Message);

                try { WriteText(context, 500, "0"); } catch { }
            }
        }

        /// <summary>Creates an account from the sign up form and reports back on the same page.</summary>
        private void HandleSignUp(HttpListenerContext context)
        {
            if (context.Request.ContentLength64 > MaximumFormSize)
            {
                WriteHtml(context, 413, SignUpPage.Form("That was too long to be a sign in name.", null));
                return;
            }

            Dictionary<string, string> form = ParseForm(
                Encoding.UTF8.GetString(ReadAll(context.Request.InputStream, MaximumFormSize)));

            string username = (Value(form, "username") ?? "").Trim();
            string password = Value(form, "password") ?? "";
            string confirm = Value(form, "confirm") ?? "";

            if (!AccountManager.IsValidUsername(username))
            {
                WriteHtml(context, 400, SignUpPage.Form(
                    "A sign in name can only use letters, numbers, dots, dashes and underscores, " +
                    "and has to be shorter than 30 characters.", username));
                return;
            }

            if (password.Length < 6)
            {
                WriteHtml(context, 400, SignUpPage.Form(
                    "Pick a password of at least six characters.", username));
                return;
            }

            if (password != confirm)
            {
                WriteHtml(context, 400, SignUpPage.Form(
                    "The two passwords do not match.", username));
                return;
            }

            bool created;

            lock (registrationLocker)
            {
                if (registrationAccounts == null)
                {
                    registrationAccounts = new AccountManager();
                }

                // Checked and inserted under the same lock, so two people signing up at the same
                // moment cannot both be told the name was free.
                if (registrationAccounts.IsUserInDatabase(username))
                {
                    created = false;
                }
                else
                {
                    registrationAccounts.InsertNewAccount(username, password);
                    created = true;
                }
            }

            if (!created)
            {
                WriteHtml(context, 409, SignUpPage.Form(
                    "Somebody already signs in with that name.", username));
                return;
            }

            Program.WriteToConsole("Account " + username + " signed up from " +
                context.Request.RemoteEndPoint.Address);

            WriteHtml(context, 200, SignUpPage.Done(username));
        }

        /// <summary>
        /// Where the client should send people who click "Sign up." An explicit registration_url is
        /// always what gets advertised, whether it is a proxy sitting in front of the page below or
        /// a different site entirely; registration_enabled only decides whether this server hosts a
        /// page of its own.
        /// </summary>
        public static string GetRegistrationUrl()
        {
            if (!string.IsNullOrWhiteSpace(Config.Properties.REGISTRATION_URL))
            {
                return Config.Properties.REGISTRATION_URL.Trim();
            }

            if (!Config.Properties.REGISTRATION_ENABLE || Config.Properties.HTTP_PORT <= 0)
            {
                return "";
            }

            // The host is left out on purpose. Only the client knows which address it reached this
            // server on, so it fills that in; hard-coding one here breaks every other route in.
            return ":" + Config.Properties.HTTP_PORT + "/signup";
        }

        private static string Value(Dictionary<string, string> form, string key)
        {
            string value;

            return form.TryGetValue(key, out value) ? value : null;
        }

        private static Dictionary<string, string> ParseForm(string body)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string pair in body.Split('&'))
            {
                if (pair.Length == 0)
                {
                    continue;
                }

                int separator = pair.IndexOf('=');

                string name = separator < 0 ? pair : pair.Substring(0, separator);
                string value = separator < 0 ? "" : pair.Substring(separator + 1);

                values[Uri.UnescapeDataString(name.Replace('+', ' '))] =
                    Uri.UnescapeDataString(value.Replace('+', ' '));
            }

            return values;
        }

        private static void WriteHtml(HttpListenerContext context, int statusCode, string html)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(html);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        /// <summary>Stores an uploaded picture and replies with the name it was saved under.</summary>
        private void HandleUpload(HttpListenerContext context)
        {
            string contentType = context.Request.ContentType ?? "";

            if (contentType.IndexOf("multipart/form-data", StringComparison.OrdinalIgnoreCase) < 0)
            {
                WriteText(context, 400, "0");
                return;
            }

            if (context.Request.ContentLength64 > MaximumUploadSize)
            {
                WriteText(context, 413, "0");
                return;
            }

            byte[] body = ReadAll(context.Request.InputStream, MaximumUploadSize + 1);

            if (body.Length > MaximumUploadSize)
            {
                WriteText(context, 413, "0");
                return;
            }

            string boundary = GetBoundary(contentType);
            string fileName;
            byte[] fileData;

            if (boundary == null || !TryExtractFile(body, boundary, out fileName, out fileData))
            {
                WriteText(context, 400, "0");
                return;
            }

            string extension = Path.GetExtension(fileName ?? "").ToLowerInvariant();

            if (Array.IndexOf(AllowedExtensions, extension) < 0)
            {
                WriteText(context, 415, "0");
                return;
            }

            string storedName = GenerateRandomString(14) + extension;

            File.WriteAllBytes(Path.Combine(uploadsDirectory, storedName), fileData);

            Program.WriteToConsole("Stored avatar " + storedName + " (" + fileData.Length + " bytes)");

            WriteText(context, 200, storedName);
        }

        private void HandleDownload(HttpListenerContext context, string name)
        {
            // Only ever serve a plain file name out of the uploads directory.
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.Contains("..") || name.Contains("/") || name.Contains("\\"))
            {
                WriteText(context, 400, "0");
                return;
            }

            string path = Path.Combine(uploadsDirectory, name);

            if (!File.Exists(path))
            {
                WriteText(context, 404, "0");
                return;
            }

            byte[] bytes = File.ReadAllBytes(path);

            context.Response.StatusCode = 200;
            context.Response.ContentType = GetContentType(Path.GetExtension(path));
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        #region Helpers

        private static string GetContentType(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".png":
                    return "image/png";
                case ".gif":
                    return "image/gif";
                default:
                    return "image/jpeg";
            }
        }

        private static void WriteText(HttpListenerContext context, int statusCode, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        private static byte[] ReadAll(Stream stream, int limit)
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                byte[] chunk = new byte[81920];
                int read;

                while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    buffer.Write(chunk, 0, read);

                    if (buffer.Length > limit)
                    {
                        break;
                    }
                }

                return buffer.ToArray();
            }
        }

        private static string GetBoundary(string contentType)
        {
            foreach (string part in contentType.Split(';'))
            {
                string trimmed = part.Trim();

                if (trimmed.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.Substring("boundary=".Length).Trim('"');
                }
            }

            return null;
        }

        /// <summary>
        /// Pulls the "file" part out of a multipart body. Kept deliberately small: the only client
        /// is this application's own uploader.
        /// </summary>
        private static bool TryExtractFile(byte[] body, string boundary, out string fileName, out byte[] fileData)
        {
            fileName = null;
            fileData = null;

            byte[] delimiter = Encoding.ASCII.GetBytes("--" + boundary);
            byte[] headerEnd = Encoding.ASCII.GetBytes("\r\n\r\n");

            int position = IndexOf(body, delimiter, 0);

            while (position >= 0)
            {
                int partStart = position + delimiter.Length;
                int next = IndexOf(body, delimiter, partStart);

                if (next < 0)
                {
                    break;
                }

                int headersEnd = IndexOf(body, headerEnd, partStart);

                if (headersEnd < 0 || headersEnd > next)
                {
                    position = next;
                    continue;
                }

                string headers = Encoding.UTF8.GetString(body, partStart, headersEnd - partStart);

                if (string.Equals(GetDispositionValue(headers, "name"), "file", StringComparison.OrdinalIgnoreCase))
                {
                    int contentStart = headersEnd + headerEnd.Length;

                    // The boundary is preceded by a CRLF that is not part of the content.
                    int contentEnd = next - 2;

                    if (contentEnd < contentStart)
                    {
                        return false;
                    }

                    fileName = GetDispositionValue(headers, "filename");

                    fileData = new byte[contentEnd - contentStart];
                    Buffer.BlockCopy(body, contentStart, fileData, 0, fileData.Length);

                    return true;
                }

                position = next;
            }

            return false;
        }

        /// <summary>
        /// Reads a Content-Disposition parameter. The value may or may not be quoted: .NET's own
        /// multipart writer leaves it bare, browsers and curl quote it.
        /// </summary>
        private static string GetDispositionValue(string headers, string parameterName)
        {
            string marker = parameterName + "=";
            int searchFrom = 0;

            while (true)
            {
                int start = headers.IndexOf(marker, searchFrom, StringComparison.OrdinalIgnoreCase);

                if (start < 0)
                {
                    return null;
                }

                // Make sure this is a whole parameter and not the tail of another
                // ("filename=" would otherwise match a search for "name=").
                bool atParameterStart = start == 0 ||
                    headers[start - 1] == ';' || headers[start - 1] == ' ' || headers[start - 1] == '\n';

                if (!atParameterStart)
                {
                    searchFrom = start + marker.Length;
                    continue;
                }

                int valueStart = start + marker.Length;

                if (valueStart < headers.Length && headers[valueStart] == '"')
                {
                    valueStart++;

                    int end = headers.IndexOf('"', valueStart);

                    return end < 0 ? null : headers.Substring(valueStart, end - valueStart);
                }

                int terminator = headers.IndexOfAny(new[] { ';', '\r', '\n' }, valueStart);

                if (terminator < 0)
                {
                    terminator = headers.Length;
                }

                return headers.Substring(valueStart, terminator - valueStart).Trim();
            }
        }

        private static int IndexOf(byte[] haystack, byte[] needle, int startIndex)
        {
            for (int i = startIndex; i <= haystack.Length - needle.Length; i++)
            {
                bool matched = true;

                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string GenerateRandomString(int length)
        {
            const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

            byte[] bytes = new byte[length];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);

            StringBuilder builder = new StringBuilder(length);

            foreach (byte value in bytes)
            {
                builder.Append(alphabet[value % alphabet.Length]);
            }

            return builder.ToString();
        }

        #endregion
    }
}
