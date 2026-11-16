using System.Printing;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Xps;
using Serilog;

namespace Pos.App.Services;

/// <summary>
/// Thin wrapper around installed Windows print queues. The POS only needs to
/// list printers, send a small test page, and later send plain-text receipts.
/// </summary>
public sealed class PrinterService
{
    public IReadOnlyList<string> GetPrinterNames()
    {
        try
        {
            using var server = new LocalPrintServer();
            return server
                .GetPrintQueues(new[]
                {
                    EnumeratedPrintQueueTypes.Local,
                    EnumeratedPrintQueueTypes.Connections
                })
                .Select(queue => queue.FullName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Could not enumerate installed printers");
            return Array.Empty<string>();
        }
    }

    public void PrintText(string? printerName, string title, string text)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(24),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            ColumnWidth = double.PositiveInfinity
        };
        document.Blocks.Add(new Paragraph(new Run(text))
        {
            Margin = new Thickness(0)
        });

        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.PageSize = new Size(793, 1122);

        using var server = new LocalPrintServer();
        PrintQueue queue;
        if (string.IsNullOrWhiteSpace(printerName))
        {
            queue = server.DefaultPrintQueue;
        }
        else
        {
            queue = new PrintQueue(server, printerName);
        }

        var writer = PrintQueue.CreateXpsDocumentWriter(queue);
        writer.Write(paginator);
        Log.Information("Printed {Title} to {Printer}", title, queue.FullName);
    }
}
