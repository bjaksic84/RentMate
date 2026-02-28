namespace RentMate.Models.Dto
{
    /// <summary>
    /// Result of a bulk file upload operation.
    /// When any upload fails, all successfully uploaded files are cleaned up automatically.
    /// </summary>
    public class FileUploadResult
    {
        public List<string> SuccessfulUrls { get; set; } = new();
        public List<string> FailedFileNames { get; set; } = new();
        public bool AllSucceeded => FailedFileNames.Count == 0;
    }
}
