using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Coms.Web.Controllers
{
    /// <summary>Shared plumbing: flash messages and Result-to-ModelState mapping.</summary>
    public abstract class ComsController : Controller
    {
        public const string FlashKey = "Flash";
        public const string FlashTypeKey = "FlashType";

        protected void Flash(string message)
        {
            TempData[FlashKey] = message;
            TempData[FlashTypeKey] = "ok";
        }

        protected void FlashError(string message)
        {
            TempData[FlashKey] = message;
            TempData[FlashTypeKey] = "error";
        }

        /// <summary>
        /// Copies a failed result into ModelState. Field-level validation
        /// errors attach to their fields; everything else becomes a summary
        /// line.
        /// </summary>
        protected void AddErrors(Result result, string? fieldPrefix = null)
        {
            if (result.IsSuccess)
            {
                return;
            }

            if (result.Code == ErrorCode.Validation && result.Errors.Count > 0)
            {
                foreach (ValidationError error in result.Errors)
                {
                    string key = string.IsNullOrEmpty(fieldPrefix) ? error.Field : fieldPrefix + "." + error.Field;
                    ModelState.AddModelError(key, error.Message);
                }

                return;
            }

            ModelState.AddModelError(string.Empty, result.Message);
        }

        protected static byte[] ParseRowVersion(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                return Array.Empty<byte>();
            }

            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return Array.Empty<byte>();
            }
        }

        protected static bool IsModelValid(ModelStateDictionary modelState)
        {
            return modelState.IsValid;
        }

        /// <summary>
        /// Builds a CSV download. UTF-8 with a byte order mark so Excel opens
        /// it with the right encoding; the file name gets a date stamp.
        /// </summary>
        protected async Task<FileContentResult> CsvFileAsync(string baseName, Func<TextWriter, Task> write)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 4096, leaveOpen: true))
                {
                    await write(writer);
                    await writer.FlushAsync();
                }

                string fileName = baseName + "-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".csv";
                return File(stream.ToArray(), "text/csv; charset=utf-8", fileName);
            }
        }
    }
}
