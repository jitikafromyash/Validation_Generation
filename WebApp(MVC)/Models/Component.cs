namespace WebApp_MVC_.Models
{
    public class Component
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public string Type { get; set; }
        public string SourceFile { get; set; }      // Add this
        public bool IsFromPagesFolder { get; set; } // Add this
        public string RelativePath { get; set; }
    }
}
