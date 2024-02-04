using System.Numerics;

namespace JsonStuff {
    public class PackageJson {
        public String? Name { get; set; }
        public String? Main { get; set; }
        public WindowJson? Window { get; set; }
    }

    public class WindowJson {
        public String? Title { get; set; }
    }

    public class GameJson {
        public Int64? Id { get; set; }
        public String? Title { get; set; }
    }

    public class UploadJson {
        public Boolean? Demo { get; set; }
        public Boolean? P_android { get; set; }
        public Boolean? P_windows { get; set; }
        public Boolean? P_osx { get; set; }
        public Boolean? P_linux { get; set; }
        public Int64? Id { get; set; }
        public String? Type { get; set; }
    }

    public class UploadsJson {
        public List<UploadJson>? Uploads { get; set; }
    }

    public class DownloadJson {
        public String? Url { get; set; }
    }
}