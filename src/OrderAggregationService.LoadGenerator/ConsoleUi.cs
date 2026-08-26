using System.Globalization;
using System.Runtime.InteropServices;

namespace OrderAggregationService.LoadGenerator;

/// <summary>
/// The generator's console: a colour-coded header with the current settings, a start
/// menu that highlights the setting being edited, and a live counter block that is
/// rewritten in place.
/// </summary>
/// <remarks>
/// Nothing scrolls. Every frame is written to the same coordinates so the window stays
/// a dashboard rather than a log, which is what makes a long run readable.
/// </remarks>
internal static class ConsoleUi
{
    private const int Width = 68;

    // The web UI's palette (wwwroot/app.css), carried over exactly where the terminal
    // supports 24-bit colour, with the nearest ConsoleColor as the fallback.
    private static readonly Rgb Accent = new(198, 163, 74, ConsoleColor.Yellow);        // --color-brass
    private static readonly Rgb Heading = new(111, 90, 30, ConsoleColor.DarkYellow);    // --color-dark-gold
    private static readonly Rgb Muted = new(217, 210, 195, ConsoleColor.DarkGray);      // --color-warm-stone
    private static readonly Rgb Value = new(244, 241, 230, ConsoleColor.White);         // --color-bone
    private static readonly Rgb Alert = new(224, 112, 112, ConsoleColor.Red);           // --status-error

    private static readonly bool Ansi = TryEnableAnsi();

    /// <summary>
    /// True when the console owns a real screen buffer. Redirected output has none, so
    /// clearing and cursor positioning throw there; the display degrades to plain lines.
    /// </summary>
    private static bool IsInteractive =>
        !Console.IsOutputRedirected && !Console.IsInputRedirected;

    /// <summary>
    /// Shows the start menu and lets the operator adjust the run before it begins.
    /// </summary>
    /// <param name="options">The options parsed from the command line.</param>
    /// <returns>The options to run with, or null when the operator quit.</returns>
    public static LoadGeneratorOptions? RunStartMenu(LoadGeneratorOptions options)
    {
        // Nothing to drive the menu with when input is piped; run what was asked for.
        if (!IsInteractive)
        {
            return options;
        }

        // Zero means nothing is being edited; otherwise the row shown in the accent
        // colour, which is what tells the operator which key registered.
        var selected = 0;

        while (true)
        {
            DrawMenu(options, selected);

            if (selected == 0)
            {
                var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
                    return options;
                }

                // Switching on the character rather than the key: the number row
                // reports D1..D6 while the numeric keypad reports NumPad1..NumPad6,
                // and both send the same character.
                var pressed = char.ToUpperInvariant(key.KeyChar);

                if (pressed == 'Q')
                {
                    return null;
                }

                if (pressed is >= '1' and <= '6')
                {
                    selected = pressed - '0';
                }

                continue;
            }

            // A row is highlighted: take its new value, then drop the highlight so the
            // next pass redraws the menu in its resting colours.
            options = Edit(options, selected);
            selected = 0;
        }
    }

    /// <summary>
    /// Draws the fixed part of the run screen and returns the row the live block starts on.
    /// </summary>
    /// <param name="options">The settings the run started with.</param>
    /// <returns>The console row the live counters occupy.</returns>
    public static int DrawRunHeader(LoadGeneratorOptions options)
    {
        if (IsInteractive)
        {
            Console.Clear();
        }

        WriteBanner();
        Console.WriteLine();
        WriteField("Target", options.BaseAddress.ToString());
        WriteField(
            "Load",
            $"up to {Number(options.RequestsPerSecond)} requests/s, jittered, {Pulse(options.PulsePeriod)}");
        WriteField(
            "Shape",
            $"up to {Number(options.OrdersPerRequest)} orders per request across {Number(options.ProductCount)} products");
        WriteField("Duration", Seconds(options.Duration, "until Ctrl+C"));
        Console.WriteLine();
        WriteRule();
        Console.WriteLine();

        return IsInteractive ? Console.CursorTop : 0;
    }

    /// <summary>
    /// Rewrites the live counters in place.
    /// </summary>
    /// <param name="row">The row returned by <see cref="DrawRunHeader"/>.</param>
    /// <param name="snapshot">The counters to show.</param>
    /// <param name="elapsed">How long the run has been going.</param>
    /// <param name="finished">True once the run has stopped.</param>
    public static void DrawCounters(int row, RunCounters snapshot, TimeSpan elapsed, bool finished)
    {
        var perSecond = elapsed.TotalSeconds > 0
            ? (snapshot.Accepted + snapshot.Rejected) / elapsed.TotalSeconds
            : 0;

        if (IsInteractive)
        {
            Console.SetCursorPosition(0, row);
        }
        else if (!finished)
        {
            // Without a cursor there is nothing to rewrite, so only the final tally is
            // printed rather than a frame every second.
            return;
        }

        WriteCounter("Elapsed", $"{elapsed.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture)} s");
        WriteCounter("Throughput", $"{perSecond.ToString("F0", CultureInfo.InvariantCulture)} requests/s");
        WriteCounter("Accepted", Number(snapshot.Accepted));
        WriteCounter("Orders", Number(snapshot.Orders));
        WriteCounter("Units", Number(snapshot.Units));
        WriteCounter("Rejected (400)", Number(snapshot.Rejected), snapshot.Rejected > 0);
        WriteCounter("Failed", Number(snapshot.Failed), snapshot.Failed > 0);
        WriteCounter("Timed out", Number(snapshot.TimedOut), snapshot.TimedOut > 0);
        WriteCounter("Shed (busy)", Number(snapshot.Shed), snapshot.Shed > 0);

        Console.WriteLine();
        WriteRule();
        WriteColoured(
            finished ? new string(' ', Width) : "  Ctrl+C stops".PadRight(Width),
            Muted);
        Console.WriteLine();
    }

    private static void DrawMenu(LoadGeneratorOptions options, int selected)
    {
        Console.Clear();
        WriteBanner();
        Console.WriteLine();
        WriteSetting(1, "Target", options.BaseAddress.ToString(), selected);
        WriteSetting(2, "Requests per second", Number(options.RequestsPerSecond), selected);
        WriteSetting(3, "Max orders per request", Number(options.OrdersPerRequest), selected);
        WriteSetting(4, "Distinct products", Number(options.ProductCount), selected);
        WriteSetting(5, "Pulse period", Seconds(options.PulsePeriod, "flat rate"), selected);
        WriteSetting(6, "Duration", Seconds(options.Duration, "until Ctrl+C"), selected);
        Console.WriteLine();
        WriteRule();

        if (selected == 0)
        {
            WriteColoured("  ENTER", Accent);
            WriteColoured("  start        ", Muted);
            WriteColoured("1-6", Accent);
            WriteColoured("  change a setting        ", Muted);
            WriteColoured("Q", Accent);
            WriteColoured("  quit", Muted);
            Console.WriteLine();
            Console.WriteLine();
            WriteColoured("  > ", Accent);
        }
    }

    private static LoadGeneratorOptions Edit(LoadGeneratorOptions options, int setting)
    {
        var (prompt, flag) = setting switch
        {
            1 => ("Target base address", "--url"),
            2 => ("Requests per second", "--rps"),
            3 => ("Max orders per request", "--orders"),
            4 => ("Distinct products", "--products"),
            5 => ("Pulse period in seconds, 0 for a flat rate", "--pulse"),
            _ => ("Duration in seconds, 0 for until Ctrl+C", "--duration"),
        };

        Console.WriteLine();
        WriteColoured($"  {prompt} ", Value);
        WriteColoured($"({flag})", Muted);
        Console.WriteLine();
        WriteColoured("  new value, ENTER keeps the current one: ", Muted);
        BeginColour(Accent);

        var value = Console.ReadLine();

        EndColour();

        if (string.IsNullOrWhiteSpace(value))
        {
            return options;
        }

        try
        {
            var trimmed = value.Trim();

            return setting switch
            {
                1 => options with { BaseAddress = new Uri(trimmed) },
                2 => options with { RequestsPerSecond = PositiveInt(trimmed) },
                3 => options with { OrdersPerRequest = PositiveInt(trimmed) },
                4 => options with { ProductCount = PositiveInt(trimmed) },
                5 => options with { PulsePeriod = NonNegativeSeconds(trimmed) },
                _ => options with { Duration = NonNegativeSeconds(trimmed) },
            };
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or UriFormatException)
        {
            Console.WriteLine();
            WriteColoured($"  {exception.Message}", Alert);
            Console.WriteLine();
            WriteColoured("  Press any key to continue.", Muted);
            Console.ReadKey(intercept: true);

            return options;
        }
    }

    private static int PositiveInt(string value) =>
        int.TryParse(value, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : throw new ArgumentException("Needs a positive whole number.");

    private static TimeSpan NonNegativeSeconds(string value) =>
        int.TryParse(value, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0
            ? TimeSpan.FromSeconds(parsed)
            : throw new ArgumentException("Needs a whole number of seconds, zero or more.");

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Pulse(TimeSpan period) =>
        period > TimeSpan.Zero
            ? $"pulsing over {period.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture)} s"
            : "at a flat rate";

    private static string Seconds(TimeSpan value, string whenZero) =>
        value > TimeSpan.Zero
            ? $"{value.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture)} s"
            : whenZero;

    private static void WriteSetting(int index, string name, string value, int selected)
    {
        var active = index == selected;

        WriteColoured("  [", active ? Accent : Muted);
        WriteColoured(index.ToString(CultureInfo.InvariantCulture), active ? Accent : Value);
        WriteColoured("]  ", active ? Accent : Muted);
        WriteColoured(name.PadRight(24), active ? Accent : Muted);
        WriteColoured(value, active ? Accent : Value);
        Console.WriteLine();
    }

    private static void WriteField(string name, string value)
    {
        WriteColoured($"  {name.PadRight(12)}", Muted);
        WriteColoured(value, Value);
        Console.WriteLine();
    }

    private static void WriteCounter(string name, string value, bool alert = false)
    {
        WriteColoured($"  {name.PadRight(18)}", Muted);
        WriteColoured(value.PadRight(Width - 20), alert ? Alert : Value);
        Console.WriteLine();
    }

    private static void WriteBanner()
    {
        // One colour for the whole block: text and frame together read as one header.
        WriteColoured(new string('=', Width) + Environment.NewLine, Accent);
        WriteColoured("  ORDER AGGREGATION - LOAD GENERATOR", Accent);
        Console.WriteLine();
        WriteColoured(new string('=', Width) + Environment.NewLine, Accent);
    }

    private static void WriteRule() =>
        WriteColoured(new string('-', Width) + Environment.NewLine, Heading);

    private static void WriteColoured(string text, Rgb colour)
    {
        BeginColour(colour);
        Console.Write(text);
        EndColour();
    }

    private static void BeginColour(Rgb colour)
    {
        if (Ansi)
        {
            Console.Write($"[38;2;{colour.R};{colour.G};{colour.B}m");
        }
        else
        {
            Console.ForegroundColor = colour.Fallback;
        }
    }

    private static void EndColour()
    {
        if (Ansi)
        {
            Console.Write("[0m");
        }
        else
        {
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Turns on virtual terminal processing so 24-bit colour sequences render. Windows
    /// Terminal has it on already; classic conhost needs the console mode flag. When it
    /// cannot be enabled, the palette falls back to the sixteen ConsoleColor values.
    /// </summary>
    /// <returns>True when ANSI sequences will be interpreted.</returns>
    private static bool TryEnableAnsi()
    {
        if (Console.IsOutputRedirected)
        {
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        const int StdOutputHandle = -11;
        const uint EnableVirtualTerminalProcessing = 0x0004;

        var handle = GetStdHandle(StdOutputHandle);

        return GetConsoleMode(handle, out var mode)
            && SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    /// <summary>
    /// A 24-bit colour with the nearest ConsoleColor for terminals without ANSI support.
    /// </summary>
    /// <param name="R">Red channel.</param>
    /// <param name="G">Green channel.</param>
    /// <param name="B">Blue channel.</param>
    /// <param name="Fallback">The ConsoleColor used when ANSI is unavailable.</param>
    private readonly record struct Rgb(byte R, byte G, byte B, ConsoleColor Fallback);
}

/// <summary>
/// A point-in-time copy of the run counters, so the display never reads a value while
/// it is being updated.
/// </summary>
/// <param name="Accepted">Requests the service accepted.</param>
/// <param name="Rejected">Requests the service rejected with 400.</param>
/// <param name="Failed">Requests that failed at the transport level.</param>
/// <param name="TimedOut">Requests the service did not answer in time.</param>
/// <param name="Shed">Batches never sent because too many were already in flight.</param>
/// <param name="Orders">Orders submitted.</param>
/// <param name="Units">Units submitted.</param>
internal readonly record struct RunCounters(
    int Accepted,
    int Rejected,
    int Failed,
    int TimedOut,
    int Shed,
    int Orders,
    long Units);
