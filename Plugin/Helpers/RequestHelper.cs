using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Plugin.Helpers
{
    public class RequestHelper
    {        
        public static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
        {
            var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;

            if (string.IsNullOrWhiteSpace(boundary))
            {
                throw new InvalidDataException();
            }

            if (boundary.Length > lengthLimit)
            {
                throw new InvalidDataException();
            }

            return boundary;
        }

        public static bool IsMultipart(string contentType)
        {
            return !string.IsNullOrEmpty(contentType)
                   && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsContentDisposition(ContentDispositionHeaderValue contentDisposition)
        {
            return contentDisposition != null
                && contentDisposition.DispositionType.Equals("form-data")
                && string.IsNullOrEmpty(contentDisposition.FileName.Value)
                && string.IsNullOrEmpty(contentDisposition.FileNameStar.Value);
        }

        private static bool IsValidFile(string fileName, Stream data, string[] permittedExtensions)
        {
            byte[] allowedChars = { };

            if (string.IsNullOrEmpty(fileName) || data == null || data.Length == 0)
            {
                return false;
            }

            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(ext) || !permittedExtensions.Contains(ext))
            {
                return false;
            }

            data.Position = 0;

            using (var reader = new BinaryReader(data))
            {
                if (ext.Equals(".txt") || ext.Equals(".csv") || ext.Equals(".prn"))
                {
                    if (allowedChars.Length == 0)
                    {
                        for (var i = 0; i < data.Length; i++)
                        {
                            if (reader.ReadByte() > sbyte.MaxValue)
                            {
                                return false;
                            }
                        }
                    }
                    else
                    {
                        for (var i = 0; i < data.Length; i++)
                        {
                            var b = reader.ReadByte();
                            if (b > sbyte.MaxValue ||
                                !allowedChars.Contains(b))
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }

            Dictionary<string, List<byte[]>> FileSignature = new Dictionary<string, List<byte[]>>
            {
                { ".gif", new List<byte[]> { new byte[] { 0x47, 0x49, 0x46, 0x38 } } },
                { ".png", new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
                { ".jpeg", new List<byte[]>
                    {
                        new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
                        new byte[] { 0xFF, 0xD8, 0xFF, 0xE2 },
                        new byte[] { 0xFF, 0xD8, 0xFF, 0xE3 },
                    }
                },
                { ".jpg", new List<byte[]>
                    {
                        new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
                        new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 },
                        new byte[] { 0xFF, 0xD8, 0xFF, 0xE8 },
                    }
                },
                { ".zip", new List<byte[]>
                    {
                        new byte[] { 0x50, 0x4B, 0x03, 0x04 },
                        new byte[] { 0x50, 0x4B, 0x4C, 0x49, 0x54, 0x45 },
                        new byte[] { 0x50, 0x4B, 0x53, 0x70, 0x58 },
                        new byte[] { 0x50, 0x4B, 0x05, 0x06 },
                        new byte[] { 0x50, 0x4B, 0x07, 0x08 },
                        new byte[] { 0x57, 0x69, 0x6E, 0x5A, 0x69, 0x70 },
                    }
                },
            };


        var signatures = FileSignature[ext];
                var headerBytes = reader.ReadBytes(signatures.Max(m => m.Length));

                return signatures.Any(signature =>
                    headerBytes.Take(signature.Length).SequenceEqual(signature));
            }
        }

        public static async Task<byte[]> Process(MultipartSection section, ContentDispositionHeaderValue contentDisposition,
            ModelStateDictionary modelState, string[] permittedExtensions, long sizeLimit)
        {
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    await section.Body.CopyToAsync(memoryStream);

                    if (memoryStream.Length == 0)
                    {
                        modelState.AddModelError("File", "The file is empty");
                    }
                    else if (memoryStream.Length > sizeLimit)
                    {
                        var megabyteSizeLimit = sizeLimit / 1048576;
                        modelState.AddModelError("File", $"The file exceeds its limitation");
                    }
                    else if (!IsValidFile(contentDisposition.FileName.Value, memoryStream, permittedExtensions))
                    {
                        modelState.AddModelError("File", "The file type isn't permitted");
                    }
                    else
                    {
                        return memoryStream.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                modelState.AddModelError("File", "The upload failed");
                
            }

            return Array.Empty<byte>();
        }

        public static async Task<byte[]> ProcessFile(MultipartSection section, ContentDispositionHeaderValue contentDisposition,
                ModelStateDictionary modelState, string[] permittedExtensions, long sizeLimit)
        {
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    await section.Body.CopyToAsync(memoryStream);

                    if (memoryStream.Length == 0)
                    {
                        modelState.AddModelError("File", "The file is empty");
                    }
                    else if (memoryStream.Length > sizeLimit)
                    {
                        var megabyteSizeLimit = sizeLimit / 1048576;
                        modelState.AddModelError("File","The file exceeds");
                    }
                    else if (!IsValidFile(contentDisposition.FileName.Value, memoryStream, permittedExtensions))
                    {
                        modelState.AddModelError("File", "The file type isn't permitted");
                    }
                    else
                    {
                        return memoryStream.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                modelState.AddModelError("File", "The upload failed");                
            }

            return Array.Empty<byte>();
        }


        public static async Task<Image> LoadImageFromUrl(string url)
        {
            Image image = null;

            try
            {
                using (HttpClient httpClient = new HttpClient())
                using (HttpResponseMessage response = await httpClient.GetAsync(url))
                using (Stream inputStream = await response.Content.ReadAsStreamAsync())
                using (Bitmap temp = new Bitmap(inputStream))
                    image = new Bitmap(temp);
            }

            catch
            {
                // Add error logging here
            }

            return image;
        }

        public static Image CropImage(Image sourceImage, int sourceX, int sourceY, int sourceWidth, int sourceHeight, int destinationWidth, int destinationHeight)
        {
            Image destinationImage = new Bitmap(destinationWidth, destinationHeight);

            using (Graphics g = Graphics.FromImage(destinationImage))
                g.DrawImage(
                  sourceImage,
                  new Rectangle(0, 0, destinationWidth, destinationHeight),
                  new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                  GraphicsUnit.Pixel
                );

            return destinationImage;
        }

    }
}
