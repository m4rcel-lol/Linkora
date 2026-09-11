using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia.Threading;

using WLMClient.UI.Windows;
using WLMData.Data.Packets;
using WLMData.Enums;

namespace WLMClient.Locale
{
    /// <summary>
    /// Keeps track of the call this client is on and matches the signals coming from the server to
    /// the call window showing them. Only one call at a time, which is what the server enforces too.
    /// </summary>
    static class CallManager
    {
        private static CallWindow current;
        private static Compat.Platform.LoopingSound ringtone;
        private static Audio.VoiceSession voice;

        /// <summary>Whether a call is being set up or is under way.</summary>
        public static bool IsBusy
        {
            get { return current != null; }
        }

        /// <summary>Starts calling a contact.</summary>
        public static void Place(UserInfo contact)
        {
            if (contact == null)
            {
                return;
            }

            if (current != null)
            {
                current.Activate();

                return;
            }

            current = Open(contact, false);

            Network.Client.SendCallSignal(contact.id, CallSignalType.invite);
        }

        /// <summary>Answers the call that is ringing.</summary>
        public static void Accept(string contactId)
        {
            if (current == null)
            {
                return;
            }

            StopRingtone();

            Network.Client.SendCallSignal(contactId, CallSignalType.accept);

            StartVoice(contactId);

            current.SetState(CallState.Connected);
        }

        /// <summary>
        /// Tells the other party this side is finished. Declining a call that never connected and
        /// hanging up a connected one are different signals to them.
        /// </summary>
        public static void LocalHangUp(string contactId, CallState previousState)
        {
            StopRingtone();
            StopVoice();

            CallSignalType signal = previousState == CallState.Ringing
                ? CallSignalType.decline
                : CallSignalType.end;

            Network.Client.SendCallSignal(contactId, signal);

            current = null;
        }

        /// <summary>Handles a call signal that arrived from the server.</summary>
        public static void Handle(CallSignal signal)
        {
            Dispatcher.UIThread.Post(() =>
            {
                switch ((CallSignalType)signal.signal)
                {
                    case CallSignalType.invite:
                        Incoming(signal.id);
                        break;

                    case CallSignalType.accept:
                        if (Matches(signal.id))
                        {
                            StartVoice(signal.id);

                            current.SetState(CallState.Connected);
                        }

                        break;

                    case CallSignalType.decline:
                        Finish(signal.id, "call.state.declined");
                        break;

                    case CallSignalType.end:
                        Finish(signal.id, "call.state.ended");
                        break;

                    case CallSignalType.busy:
                        Finish(signal.id, "call.state.busy");
                        break;

                    case CallSignalType.unavailable:
                        Finish(signal.id, "call.state.unavailable");
                        break;
                }
            });
        }

        private static void Incoming(string contactId)
        {
            UserInfo contact = Personal.USER_CONTACTS.FirstOrDefault(
                x => string.Equals(x.id.Trim(), contactId.Trim(), StringComparison.OrdinalIgnoreCase));

            if (contact == null)
            {
                // Somebody not on the contact list; turn it down rather than ringing for a stranger.
                Network.Client.SendCallSignal(contactId, CallSignalType.decline);

                return;
            }

            if (current != null)
            {
                Network.Client.SendCallSignal(contactId, CallSignalType.decline);

                return;
            }

            current = Open(contact, true);

            // Rings until the call is answered, declined or given up on.
            ringtone = Resource.Sounds.Player.StartLoop(Resource.Sounds.Identifiers.RING);

            Notification.NotificationManager.Showpopup(contact.name, Language.Get("call.incoming"), null);
        }

        private static bool Matches(string contactId)
        {
            return current != null &&
                string.Equals(current.ContactId.Trim(), (contactId ?? "").Trim(),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void Finish(string contactId, string reasonKey)
        {
            if (!Matches(contactId))
            {
                return;
            }

            StopRingtone();
            StopVoice();

            CallWindow window = current;
            current = null;

            window.ShowEnded(reasonKey);
        }

        private static CallWindow Open(UserInfo contact, bool incoming)
        {
            CallWindow window = new CallWindow(contact, incoming);

            window.Finished += Closed;
            window.Show();
            window.Activate();

            return window;
        }

        private static void Closed(CallWindow window)
        {
            StopRingtone();
            StopVoice();

            if (ReferenceEquals(current, window))
            {
                current = null;
            }
        }

        /// <summary>Opens the microphone and speakers for a call that has just connected.</summary>
        private static void StartVoice(string contactId)
        {
            StopVoice();

            Audio.VoiceSession session = new Audio.VoiceSession(contactId);
            session.Start();

            voice = session;

            if (current != null)
            {
                current.ShowAudioState(session.HasMicrophone, session.HasSpeakers);
            }
        }

        private static void StopVoice()
        {
            Audio.VoiceSession session = voice;
            voice = null;

            if (session != null)
            {
                session.Dispose();
            }
        }

        /// <summary>Hands a frame of call audio to playback.</summary>
        public static void ReceiveVoice(VoiceFrame frame)
        {
            Audio.VoiceSession session = voice;

            if (session == null || frame == null)
            {
                return;
            }

            session.Receive(frame.data);
        }

        /// <summary>Mutes or unmutes the microphone for the call in progress.</summary>
        public static void SetMuted(bool muted)
        {
            Audio.VoiceSession session = voice;

            if (session != null)
            {
                session.Muted = muted;
            }
        }

        /// <summary>Silences the ringtone, whatever ended the ringing.</summary>
        private static void StopRingtone()
        {
            Compat.Platform.LoopingSound sound = ringtone;
            ringtone = null;

            if (sound != null)
            {
                sound.Stop();
            }
        }

        /// <summary>Drops any call, used when the connection to the server goes away.</summary>
        public static void Reset()
        {
            StopRingtone();
            StopVoice();

            CallWindow window = current;
            current = null;

            if (window != null)
            {
                try
                {
                    window.Close();
                }
                catch
                {
                    // Already gone.
                }
            }
        }
    }
}
