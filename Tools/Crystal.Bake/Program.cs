using System.Text.Json;
using Crystal.Assets;
using Crystal.Assets.Atlas;
using Crystal.Assets.Parsers;
using Crystal.Assets.SampleData;

namespace Crystal.Bake;

internal static class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            return args[0] switch
            {
                "init-sample" => InitSample(args.Skip(1).ToArray()),
                "bake" => Bake(args.Skip(1).ToArray()),
                _ => Bake(args)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 2;
        }
    }

    static int InitSample(string[] args)
    {
        string dest = args.Length > 0 ? args[0] : Path.Combine("Tools", "Crystal.Bake", "fixtures", "Data");
        SampleDataWriter.Write(dest);
        Console.WriteLine($"Wrote sample Data tree to {Path.GetFullPath(dest)}");
        return 0;
    }

    static int Bake(string[] args)
    {
        var opts = ParseOptions(args);
        if (string.IsNullOrWhiteSpace(opts.DataRoot))
        {
            Console.Error.WriteLine("Usage: crystal-bake bake --data <DataDir> [--out <dir>] [--atlas-size 2048] [--compress bc3|none]");
            return 1;
        }

        if (!Directory.Exists(opts.DataRoot))
        {
            Console.Error.WriteLine($"Data directory does not exist: {opts.DataRoot}");
            return 1;
        }

        Directory.CreateDirectory(opts.Output);

        var discovered = DataTreeWalker.Enumerate(opts.DataRoot);
        Console.WriteLine($"Enumerated {discovered.Count} libraries under {Path.GetFullPath(opts.DataRoot)}");

        var parsed = new List<LibraryParseResult>(discovered.Count);
        foreach (var lib in discovered)
        {
            var result = LibraryParser.Parse(lib);
            parsed.Add(result);
            string status = result.HeaderParsed
                ? $"ok {result.Kind} images={result.ImageCount} decoded={result.DecodedCount}"
                : $"FAIL {result.Error}";
            Console.WriteLine($"  {result.RelativePath}: {status}");
        }

        var catalog = AtlasPacker.Pack(opts.DataRoot, parsed, new AtlasPackerOptions
        {
            AtlasSize = opts.AtlasSize,
            CompressBc3 = opts.CompressBc3,
            OutputDirectory = opts.Output
        });

        var coverage = CoverageBuilder.Build(opts.DataRoot, discovered, parsed, catalog);

        var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(opts.Output, "catalog.json"), JsonSerializer.Serialize(catalog, jsonOpts));
        File.WriteAllText(Path.Combine(opts.Output, "bake-coverage.json"), JsonSerializer.Serialize(coverage, jsonOpts));

        Console.WriteLine();
        Console.WriteLine("=== Coverage ===");
        Console.WriteLine($"libraries parsed/total : {coverage.LibrariesParsed}/{coverage.LibrariesDiscovered} ({coverage.LibraryParsePercent:0.##}%)");
        Console.WriteLine($"images decoded/listed  : {coverage.ImagesDecoded}/{coverage.ImagesListed} ({coverage.ImageDecodePercent:0.##}%)");
        Console.WriteLine($"images packed/decoded  : {coverage.ImagesPacked}/{coverage.ImagesDecoded} ({coverage.ImagePackPercent:0.##}%)");
        Console.WriteLine($"catalog present/expect : {coverage.CatalogPresent}/{coverage.CatalogExpected} ({coverage.CatalogPresentPercent:0.##}%)");
        Console.WriteLine($"atlases written        : {catalog.Atlases.Count}");
        Console.WriteLine($"report                 : {Path.Combine(opts.Output, "bake-coverage.json")}");
        return coverage.LibrariesDiscovered > 0 && coverage.LibrariesParsed == 0 ? 3 : 0;
    }

    static BakeOptions ParseOptions(string[] args)
    {
        var opts = new BakeOptions();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--data":
                case "-d":
                    opts.DataRoot = Next(args, ref i);
                    break;
                case "--out":
                case "-o":
                    opts.Output = Next(args, ref i);
                    break;
                case "--atlas-size":
                    opts.AtlasSize = int.Parse(Next(args, ref i));
                    break;
                case "--compress":
                    opts.CompressBc3 = !string.Equals(Next(args, ref i), "none", StringComparison.OrdinalIgnoreCase);
                    break;
                default:
                    if (opts.DataRoot == null && !args[i].StartsWith('-'))
                        opts.DataRoot = args[i];
                    break;
            }
        }
        return opts;
    }

    static string Next(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"Missing value after {args[i]}");
        return args[++i];
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
            crystal-bake — Crystal Data tree → atlas packs + coverage

            Commands:
              init-sample [dir]     Write a miniature Data tree (not game art)
              bake --data <dir>     Enumerate, parse, pack, report

            bake options:
              --data, -d <dir>      Crystal Data root (required)
              --out,  -o <dir>      Output directory (default ./bake-out)
              --atlas-size <n>      Atlas edge in pixels (default 2048)
              --compress bc3|none   GPU compression (default bc3)

            Coverage is parsed/total of files that exist. Missing catalog slots
            are listed; art is never invented.
            """);
    }

    sealed class BakeOptions
    {
        public string? DataRoot { get; set; }
        public string Output { get; set; } = "bake-out";
        public int AtlasSize { get; set; } = 2048;
        public bool CompressBc3 { get; set; } = true;
    }
}
