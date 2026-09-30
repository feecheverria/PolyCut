using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PolyCut;
using PolyCut.Core;

internal static class Program
{
    private static int passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine($"PASS {++passed}: {name}");
    }
    private static void Reject(Action action, string name)
    {
        try { action(); }
        catch (InvalidOperationException) { Check(true, name); return; }
        throw new Exception($"Expected rejection: {name}");
    }
    private static ProcessorConfiguration Config() => new()
    {
        WorkAreaWidth = 235, WorkAreaHeight = 235, ToolOffsetX = 25, ToolOffsetY = -2,
        WorkZ = 1, TravelZ = 6, WorkSpeed = 15, TravelSpeed = 30, ZSpeed = 3,
        PlotterSafety = new() { CalibrationConfirmed = true },
        Paper = new() { Left = 40, Top = 60, Width = 90, Height = 130 }
    };
    private static List<GeoLine> Lines() => [new(50, 70, 100, 70), new(100, 70, 100, 150)];

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            CoreChecks();
            if (args.Length > 0 && args[0] == "--ui") UiChecks(args[1]);
            Console.WriteLine($"All {passed} checks passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void CoreChecks()
    {
        var cfg = Config();
        var job = GCodeGenerator.Generate(Lines(), cfg);
        Check(job.GCodes[0].Z == 6 && !job.GCodes[0].X.HasValue, "raise Z before any XY move");
        Check(job.GCodes[1].X == 75 && job.GCodes[1].Y == 163, "physical left offset: nozzle = tip - physical offset");
        Check(job.GCodes.Where(g => g.Z.HasValue).All(g => g.F == 180), "explicit Z feedrate");
        Check(job.GCodes.Last().Z == 6, "finish with pen raised");
        var square = new List<GeoLine> { new(50, 70, 80, 70), new(80, 70, 80, 100), new(80, 100, 50, 100), new(50, 100, 50, 70) };
        cfg.SelectedToolMode = ProcessorConfiguration.ToolMode.Cut;
        cfg.Passes = 2;
        var repeated = GCodeGenerator.Generate(square, cfg).GCodes;
        var returnIndex = repeated.FindLastIndex(g => g.Code == 0 && g.X == 75 && g.Y == 163);
        Check(returnIndex > 0 && repeated[returnIndex - 1].Z == 6, "closed multi-pass path raises pen before restart");
        cfg = Config();
        cfg.PlotterSafety.CalibrationConfirmed = false;
        Reject(() => GCodeGenerator.Generate(Lines(), cfg), "unmeasured profile is blocked");
        cfg = Config();
        Reject(() => GCodeGenerator.Generate([new(20, 70, 50, 70)], cfg), "tip outside safe area is blocked");
        Reject(() => GCodeGenerator.Generate([new(50, 70, 181, 70)], cfg), "unsafe endpoint blocks whole job");
        cfg.ToolOffsetX = 200;
        Reject(() => GCodeGenerator.Generate(Lines(), cfg), "compensated nozzle outside motion limits is blocked");
        cfg = Config();
        Reject(() => GCodeGenerator.Generate([new(35, 70, 50, 70)], cfg), "path outside paper is blocked");
        cfg.Paper.Width = -5;
        Reject(() => GCodeGenerator.Generate(Lines(), cfg), "negative paper size is blocked");
        cfg = Config(); cfg.TravelZ = cfg.WorkZ;
        Reject(() => GCodeGenerator.Generate(Lines(), cfg), "invalid Z clearance is blocked");
        cfg = Config(); cfg.WorkSpeed = 0;
        Reject(() => GCodeGenerator.Generate(Lines(), cfg), "zero speed is blocked");
        cfg = Config(); cfg.ToolOffsetX = double.NaN;
        Reject(() => GCodeGenerator.Generate(Lines(), cfg), "non-finite compensation is blocked");
        cfg = Config(); cfg.Passes = 2;
        Reject(() => GCodeGenerator.Generate(Lines(), cfg), "pen mode rejects cutting-style multipass");
        cfg.SelectedToolMode = ProcessorConfiguration.ToolMode.Cut; cfg.PassHeightDelta = -2;
        Reject(() => GCodeGenerator.Generate(Lines(), cfg), "cut pass below minimum Z is blocked");
        cfg = Config(); cfg.PlotterSafety.SafeTipArea = new(0, 0, 240, 240);
        Reject(() => GCodeGenerator.Generate(Lines(), cfg), "safe area beyond physical bed is blocked");
        cfg = Config();
        var grid = CalibrationPatterns.Create(cfg, true);
        Check(grid.Count == 10, "central calibration grid and border");
        Check(GCodeGenerator.Generate(grid, cfg, false).GCodes.Count > 0, "calibration bypasses paper, preserves machine limits");
        cfg.PlotterSafety.CalibrationConfirmed = false;
        Reject(() => GCodeGenerator.Generate(grid, cfg, false), "calibration also requires measured settings");
        cfg = Config();
        var compiled = PlotterJob.Compile(job.GCodes, cfg);
        Check(compiled.Contains("M104 S0") && compiled.Contains("M140 S0"), "heaters disabled");
        Check(!compiled.Contains("G28") && !job.GCodes.Any(g => g.E.HasValue), "no automatic homing or extrusion");
        Reject(() => PlotterJob.Compile([new GCode { Mode = "G", Code = 1, E = 5 }], cfg), "extrusion commands rejected");
        Reject(() => PlotterJob.Compile([new GCode { Mode = "M", Code = 109 }], cfg), "heating commands rejected");
        var clone = cfg.Clone(); clone.Paper.Width = 10; clone.PlotterSafety.CalibrationConfirmed = false;
        Check(cfg.Paper.Width == 90 && cfg.PlotterSafety.CalibrationConfirmed, "paper and safety settings clone independently");
        var printer = new Printer { ToolOffsetX = -25, ToolOffsetY = 2, ContactZ = 1, LiftZ = 6, CalibrationConfirmed = true };
        var copy = printer.Clone();
        Check(copy.ToolOffsetX == -25 && copy.ToolOffsetY == 2 && copy.ContactZ == 1 && copy.LiftZ == 6 && copy.CalibrationConfirmed, "profile clone preserves mounting calibration");
        var target = new Printer(); target.CopyFrom(printer);
        Check(target.ToolOffsetX == -25 && target.ContactZ == 1, "profile overwrite preserves calibration");
        printer.BedHeight = 200; printer.WorkingOffsetY = 160;
        Check(printer.WorkingHeight == 40, "non-square bed clamps safe area by bed height");
        printer.WorkingOffsetX = -20; printer.WorkingWidth = -20;
        Check(printer.WorkingOffsetX == 0 && printer.WorkingWidth == 0, "negative safe area inputs are clamped");
        Check(new Printer().CalibrationConfirmed == false, "new pen and cutter profiles start uncalibrated");
        CultureInfo.CurrentCulture = new("es-CL");
        Check(GCode.G1(X: 50.25, Y: 100.5).ToString().Contains("X50.25 Y100.5"), "G-code uses decimal point under Chilean locale");
    }

    private static void UiChecks(string artifacts)
    {
        Directory.CreateDirectory(artifacts);
        SettingsHandler.DataFolder = new DirectoryInfo(Path.Combine(artifacts, "profiles-" + Guid.NewGuid().ToString("N")));
        var app = new PolyCut.Application();
        app.InitializeComponent();
        Exception? failure = null;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += async (_, _) =>
        {
            if (app.MainWindow is not MainWindow window) return;
            timer.Stop();
            try
            {
                var vm = PolyCut.Application.GetService<MainViewModel>();
                Check(window.Title.Contains("End3r Draw"), "WPF application starts with End3r Draw title");
                Check(((FrameworkElement)window.FindName("MonitorPage_NavigationButton")).Visibility == Visibility.Collapsed, "Monitor hidden");
                Check(vm.Printer.Name == "Ender 3 Pro + Pen Holder", "default pen profile loaded");
                Check(vm.Printers.Count == 1, "first-run profile is registered once");
                vm.ApplyPaperPreset();
                Check(vm.Configuration.Paper.Width == 90 && vm.Configuration.Paper.Height == 130, "custom notebook preset");
                vm.Configuration.Paper.Preset = "A5"; vm.ApplyPaperPreset();
                Check(vm.Configuration.Paper.Width == 148 && vm.Configuration.Paper.Height == 210, "A5 preset");
                vm.RotatePaper(); Check(vm.Configuration.Paper.Width == 210 && vm.Configuration.Paper.Height == 148, "landscape paper");
                vm.Configuration.Paper.Preset = "Custom 13 x 9 cm"; vm.ApplyPaperPreset();
                await vm.GenerateGCodeCommand.ExecuteAsync(null);
                Check(string.IsNullOrEmpty(vm.GCode), "uncalibrated UI job is blocked");
                vm.Printer.ToolOffsetX = -25; vm.Printer.ToolOffsetY = 2;
                vm.Printer.ContactZ = 1; vm.Printer.LiftZ = 6; vm.Printer.CalibrationConfirmed = true;
                vm.Configuration.DrawingConfig.FillType = FillType.None;
                var fixture = Path.Combine(artifacts, "import.svg");
                File.WriteAllText(fixture, "<svg xmlns='http://www.w3.org/2000/svg' width='235mm' height='235mm' viewBox='0 0 235 235'><path d='M50 70 L100 70 L100 150 L50 150 Z' fill='none' stroke='black' stroke-width='0.4'/></svg>");
                vm.DragSVGs([fixture]);
                await app.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                Check(vm.HasDrawables, "SVG import attaches artwork to live canvas");
                Capture(window, Path.Combine(artifacts, "home.png"));
                await vm.GenerateGCodeCommand.ExecuteAsync(null);
                Check(!string.IsNullOrEmpty(vm.GCode), "SVG -> processed paths -> G-code in live application");
                Check(vm.CanExportCurrentJob(), "unchanged job passes export guard");
                vm.Configuration.ExportConfig.DestinationIP = "http://127.0.0.1";
                Check(vm.CanExportCurrentJob(), "export destination changes do not invalidate toolpath");
                Check(vm.GCodeGeometry.Paths.Any(p => !p.IsRapidMove && p.X1 >= 40 && p.X2 <= 130), "preview restores tip coordinates");
                File.WriteAllText(Path.Combine(artifacts, "simulated-notebook.gcode"), vm.GCode);
                await app.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                Capture(window, Path.Combine(artifacts, "preview.png"));
                vm.Configuration.Paper.Left += 1;
                Check(!vm.CanExportCurrentJob() && string.IsNullOrEmpty(vm.GCode), "edited paper invalidates exported job");
                await vm.GenerateGCodeCommand.ExecuteAsync(null);
                Check(!string.IsNullOrEmpty(vm.GCode), "valid job regenerates after an edit");
                vm.Configuration.DrawingConfig.MinStrokeWidth += 0.1;
                Check(!vm.CanExportCurrentJob(), "nested drawing settings invalidate exported job");
                var fixtureOutside = Path.Combine(artifacts, "outside.svg");
                ((Wpf.Ui.Controls.NavigationView)window.FindName("NavigationView")).Navigate(typeof(MainWindow).Assembly.GetType("PolyCut.SVGPage")!);
                await app.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                var beforeImport = vm.DrawableCollection.Count;
                File.WriteAllText(fixtureOutside, "<svg xmlns='http://www.w3.org/2000/svg' width='235mm' height='235mm' viewBox='0 0 235 235'><path d='M230 70 L250 70' fill='none' stroke='black'/></svg>");
                vm.DragSVGs([fixtureOutside]);
                await app.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                Check(vm.DrawableCollection.Count > beforeImport, "out-of-bed open path imports into the live Home canvas");
                await vm.GenerateGCodeCommand.ExecuteAsync(null);
                Check(string.IsNullOrEmpty(vm.GCode), "out-of-bed artwork blocks whole job instead of disappearing");
                var service = PolyCut.Application.GetService<ProjectSerializationService>();
                var project = Path.Combine(artifacts, "roundtrip.polycut");
                Check(service.SaveProject(project, vm.DrawableCollection, vm.ImportedGroups, vm.Configuration), "project saves artwork and paper settings");
                var data = service.LoadProject(project);
                var restored = System.Text.Json.JsonSerializer.Deserialize<ProcessorConfiguration>(data.Configuration);
                Check(restored!.Paper.Width == 90 && restored.Paper.Left == 41, "project roundtrip retains paper placement");
                vm.GenerateCalibration("grid");
                Check(!string.IsNullOrEmpty(vm.GCode) && vm.CanExportCurrentJob(), "calibration generated and previewed in app");
                vm.AddCutterProfile();
                Check(vm.Printer.AttachedToolMode == ProcessorConfiguration.ToolMode.Cut && !vm.Printer.CalibrationConfirmed, "independent uncalibrated cutter profile");
                await vm.GenerateGCodeCommand.ExecuteAsync(null);
                Check(string.IsNullOrEmpty(vm.GCode), "uncalibrated cutter cannot export");
                vm.MainViewClosing();
                var persisted = SettingsHandler.PrinterSettings.GetValue<Printer>("Ender 3 Pro + Pen Holder.json");
                Check(persisted.ToolOffsetX == -25 && persisted.ContactZ == 1, "measured tool profile persists on closing");
            }
            catch (Exception ex) { failure = ex; }
            finally { app.Shutdown(); }
        };
        app.DispatcherUnhandledException += (_, e) => { failure = e.Exception; e.Handled = true; app.Shutdown(); };
        timer.Start(); app.Run();
        if (failure is not null) throw failure;
    }

    private static void Capture(Window window, string path)
    {
        var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        // Mica lives in the native window; render a dark backing for transparent WPF brushes.
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, window.ActualWidth, window.ActualHeight);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(25, 27, 33)), null, bounds);
            dc.DrawRectangle(new VisualBrush(window), null, bounds);
        }
        image.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path); encoder.Save(file);
    }
}
