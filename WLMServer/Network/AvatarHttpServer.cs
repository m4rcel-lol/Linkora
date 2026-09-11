using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace WLMServer.Network
{
    /// <summary>
    /// A small HTTP server for profile pictures, so avatars work without a separate PHP webserver.
    /// It speaks the same two endpoints the client already expects:
    ///
    ///   POST /upload          multipart/form-data with a "file" field, replies with the stored name
    ///   GET  /uploads/&lt;name&gt;  serves a stored picture
    ///
    /// Point avatars_address and avatars_address_upload at it, or keep using upload.php instead.
    /// </summary>
    class AvatarHttpServer
    {
        /// <summary>Matches the limit the bundled upload.php enforced.</summary>
        private const int MaximumUploadSize = 5 * 1024 * 1024;

        private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".jpe", ".jfif", ".gif" };

        private readonly HttpListener listener = new HttpListener();
        private readonly string uploadsDirectory;
        private readonly int port;

        public AvatarHttpServer(int port, string uploadsDirectory)
        {
            this.port = port;
            this.uploadsDirectory = uploadsDirectory;

            Directory.CreateDirectory(uploadsDirectory);

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

            Program.WriteToConsole("Avatar HTTP server listening on port " + port +
                " (files in " + uploadsDirectory + ")");

            Thread thread = new Thread(Listen);
            thread.IsBackground = true;
            thread.Name = "Avatar HTTP server";
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

                if (context.Request.HttpMethod == "POST" && path.TrimEnd('/').EndsWith("/upload"))
                {
                    HandleUpload(context);
                }
                else if (context.Request.HttpMethod == "GET" && path.StartsWith("/uploads/"))
                {
                    HandleDownload(context, path.Substring("/uploads/".Length));
                }
                else
                {
                    WriteText(context, 404, "0");
                }
            }
            catch (Exception exception)
            {
                Program.WriteToConsole("Avatar HTTP error: " + exception.Message);

                try { WriteText(context, 500, "0"); } catch { }
            }
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
