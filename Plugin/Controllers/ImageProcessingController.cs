using System;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing;
using System.Net;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Plugin.Data;
using Plugin.Helpers;

namespace Plugin.Controllers
{
    public class ImageProcessingController : Controller
    {
        private PluginDbContext Context { get; set; }
        private long FileSize { get; set; }
        private ILogger<ImageProcessingController> Logger { get; set; }
        private string FilePath { get; set; }

        private static readonly FormOptions _defaultFormOptions = new FormOptions();

        public ImageProcessingController(ILogger<ImageProcessingController> logger, PluginDbContext context, IConfiguration config)
        {
            Logger = logger;
            Context = context;
            FileSize = config.GetValue<long>("FileSize");
            FilePath = config.GetValue<string>("FilePath");
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Upload()
        {
            if (!RequestHelper.IsMultipart(Request.ContentType))
            {
                ModelState.AddModelError("File", $"Couldn't process");
                return BadRequest(ModelState);
            }

            var boundary = RequestHelper.GetBoundary(MediaTypeHeaderValue.Parse(Request.ContentType),
                _defaultFormOptions.MultipartBoundaryLengthLimit);
            var reader = new MultipartReader(boundary, HttpContext.Request.Body);
            var section = await reader.ReadNextSectionAsync();

            while (section != null)
            {
                ContentDispositionHeaderValue contentDisposition;
                var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out contentDisposition);

                if (hasContentDispositionHeader)
                {
                    if (!RequestHelper.IsContentDisposition(contentDisposition))
                    {
                        ModelState.AddModelError("File", $"The request couldn't be processed (Error 2).");
                        
                        return BadRequest(ModelState);
                    }
                    else
                    {
                        var trustedFileNameForDisplay = WebUtility.HtmlEncode(contentDisposition.FileName.Value);
                        var trustedFileNameForFileStorage = Path.GetRandomFileName();

                        var streamedFileContent = await RequestHelper.ProcessFile(
                            section, contentDisposition, ModelState,new [] { ".txt" }, FileSize);

                        if (!ModelState.IsValid)
                        {
                            return BadRequest(ModelState);
                        }

                        using (var targetStream = System.IO.File.Create(
                            Path.Combine(FilePath, trustedFileNameForFileStorage)))
                        {
                            await targetStream.WriteAsync(streamedFileContent);

                            Logger.LogInformation("Uploaded file", trustedFileNameForDisplay, FilePath, 
                                trustedFileNameForFileStorage);
                        }
                    }
                }

                section = await reader.ReadNextSectionAsync();
            }

            return Created(nameof(ImageProcessingController), null);
        }

        [HttpPost]
        public async Task<IActionResult> Resize(string url, int sourceX, int sourceY, int sourceWidth, int sourceHeight, int destinationWidth, int destinationHeight)
        {
            using (Image sourceImage = await RequestHelper.LoadImageFromUrl(url))
            {
                if (sourceImage != null)
                {
                    try
                    {
                        using (Image destinationImage = RequestHelper.CropImage(sourceImage, sourceX, sourceY, sourceWidth, sourceHeight, destinationWidth, destinationHeight))
                        {
                            Stream outputStream = new MemoryStream();

                            destinationImage.Save(outputStream, ImageFormat.Jpeg);
                            outputStream.Seek(0, SeekOrigin.Begin);
                            return this.File(outputStream, "image/png");
                        }
                    }

                    catch
                    {
                        // Add error logging here
                    }
                }
            }

            return this.NotFound();
        }

        //[HttpPost]
        //public IActionResult Blur(Stream stream)
        //{
        //    Bitmap img = new Bitmap(stream);
        //    Bitmap blurPic = new Bitmap(img.Width, img.Height);

        //    Int32 avgR = 0, avgG = 0, avgB = 0;
        //    Int32 blurPixelCount = 0;

        //    for (int y = 0; y < img.Height; y++)
        //    {
        //        for (int x = 0; x < img.Width; x++)
        //        {
        //            Color pixel = img.GetPixel(x, y);
        //            avgR += pixel.R;
        //            avgG += pixel.G;
        //            avgB += pixel.B;

        //            blurPixelCount++;
        //        }
        //    }

        //    avgR = avgR / blurPixelCount;
        //    avgG = avgG / blurPixelCount;
        //    avgB = avgB / blurPixelCount;

        //    for (int y = 0; y < img.Height; y++)
        //    {
        //        for (int x = 0; x < img.Width; x++)
        //        {
        //            blurPic.SetPixel(x, y, Color.FromArgb(avgR, avgG, avgB));
        //        }
        //    }

        //    img = blurPic;

        //    return this.File(img, "image/png");
        //}
    }
}
