using System.Buffers.Text;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using JsonStuff;

class ItchToApp {
    private static readonly HttpClient httpClient = new();
    private static readonly String UploadsUri = "https://itch.io/api/1/{0}/game/{1}/uploads";
    private static readonly String DownloadUri = "https://itch.io/api/1/{0}/upload/{1}/download";

    public static async Task Main(String[] args) {
        if (args.Length < 2) {
            Console.WriteLine("Please give an itch.io game url as argument 1 (http://username.itch.io/game)");
            Console.WriteLine("Please give an itch.io API key as argument 2");
            return;
        }

        if (args[0].EndsWith("/")) {
            args[0] = args[0].Remove(args[0].Length - 1, 1);
        }

        HttpResponseMessage response = await httpClient.GetAsync(args[0] +  "/data.json");

        String gameJson = await response.Content.ReadAsStringAsync();

        Console.WriteLine(gameJson);

        GameJson? deserializedGameJson = JsonSerializer.Deserialize<GameJson>(gameJson, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
        if (deserializedGameJson == null) {
            return;
        }else if (deserializedGameJson.Id == null) {
            return;
        }
        
        Console.WriteLine("Got game id " + deserializedGameJson.Id);

        response = await httpClient.GetAsync(String.Format(UploadsUri, args[1], deserializedGameJson.Id));

        String uploadsJson = await response.Content.ReadAsStringAsync();
        UploadsJson? deserializedUploadsJson = JsonSerializer.Deserialize<UploadsJson>(uploadsJson, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
        if (deserializedUploadsJson == null) {
            return;
        }else if (deserializedUploadsJson.Uploads == null) {
            return;
        }
        
        UploadJson[] uploads = deserializedUploadsJson.Uploads.ToArray<UploadJson>();

        Int64? uploadId = null;

        for (int i = 0; i < uploads.Length; i++) {
            String? uploadType = uploads[i].Type;
            if (uploadType == null) {
                continue;
            }

            if (uploadType.ToLower().Equals("html")) {
                uploadId = uploads[i].Id;
            }
        }

        if (uploadId == null) {
            return;
        }

        Console.WriteLine("Got upload id " + uploadId);

        response = await httpClient.GetAsync(String.Format(DownloadUri, args[1], uploadId));

        String downloadJson = await response.Content.ReadAsStringAsync();
        DownloadJson? deserializedDownloadJson = JsonSerializer.Deserialize<DownloadJson>(downloadJson, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

        if (deserializedDownloadJson == null) {
            Console.WriteLine("test");
            return;
        }else if (deserializedDownloadJson.Url == null) {
            Console.WriteLine("test");
            return;
        }

        response = await httpClient.GetAsync(deserializedDownloadJson.Url, HttpCompletionOption.ResponseHeadersRead);

        Console.WriteLine("Downloading to game.zip");

        String zipFile = Directory.GetCurrentDirectory().ToString() + "/game.zip";

        Console.WriteLine(zipFile);

        if (!File.Exists(zipFile)) {
            File.Create(zipFile).Close();
        }
        var FileOutStream = new FileStream(zipFile, FileMode.Create);

        response.Content.ReadAsStream().CopyTo(FileOutStream);

        FileOutStream.Close();
        
        String workingDirectory = Directory.GetCurrentDirectory().ToString() + "/game/";
        
        Directory.Delete(workingDirectory, true);
        Directory.CreateDirectory(workingDirectory);

        var zip = ZipFile.Open(zipFile, ZipArchiveMode.Read);
        zip.ExtractToDirectory(workingDirectory);

        var packagejson = File.Create(workingDirectory + "package.json");

        String main = "index.html";
        if (!File.Exists(workingDirectory + main)) {
            Console.WriteLine("index.html not found, searching subdirectories");
            string[] dirs = Directory.GetDirectories(workingDirectory, "*", SearchOption.TopDirectoryOnly);

            foreach (String dir in dirs) {
                if (File.Exists(dir + "/index.html")) {
                    Console.WriteLine("Found index.html in subdirectory " + dir + ", setting main file");

                    String dirName = dir.Split("/").Last();

                    Console.WriteLine("Main set to: " + dirName + "/index.html");
                    main = dirName + "/index.html";
                }
            }
        }

        String name = args[0].Split("/").Last();

        JsonSerializerOptions options = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        String packageJson = JsonSerializer.Serialize<PackageJson>(new PackageJson{
            Name = name,
            Main = main,
            Window = new WindowJson{
                Title = deserializedGameJson.Title
            }
        }, options);

        packagejson.Write(Encoding.UTF8.GetBytes("{\"main\":\"" + main + "\",\"name\":\"" + name + "\"}").AsSpan());

        packagejson.Flush();
        packagejson.Close();

        String[] buildargs = {
            "--glob=false --platform=win --arch=x64 --outDir=out/win_x64 ./game",
            "--glob=false --platform=win --arch=ia32 --outDir=out/win_ia32 ./game",
            "--glob=false --platform=osx --arch=x64 --outDir=out/osx_x64 ./game",
            "--glob=false --platform=osx --arch=arm64 --outDir=out/osx_arm64 ./game",
            "--glob=false --platform=linux --arch=x64 --outDir=out/linux_x64 ./game",
            "--glob=false --platform=linux --arch=ia32 --outDir=out/linux_ia32 ./game"
        };

        Console.WriteLine("building");
        for (int i = 0; i < buildargs.Length; i++) {
            var psi = new ProcessStartInfo();
            psi.FileName = "nwbuild";
            psi.Arguments = buildargs[i];
            psi.UseShellExecute = true;
            psi.CreateNoWindow = false;
            psi.WorkingDirectory = Directory.GetCurrentDirectory().ToString();

            var process = Process.Start(psi);

            if (process == null) {
                Console.WriteLine("Use nw_builder to build yourself");
                return;
            }

            process.WaitForExit();
        }
    }
}

