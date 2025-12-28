using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using MindLog.Data;
using MindLog.Interfaces;
using MindLog.Helpers;
using MindLog.Models;
using Microsoft.Maui.Storage;

namespace MindLog.Services
{
    public class PdfExportService : IPdfExportService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PdfExportService> _logger;

        static PdfExportService()
        {
            try
            {
                GlobalFontSettings.FontResolver = new FontResolver();
            }
            catch (Exception ex)
            {
                Logger.GetLogger<PdfExportService>().LogError(ex, "Error initializing font resolver");
            }
        }

        public PdfExportService(AppDbContext context)
        {
            _context = context;
            _logger = Logger.GetLogger<PdfExportService>();
        }

        public async Task<byte[]> ExportJournalsToPdfAsync(int userId, DateOnly? startDate = null, DateOnly? endDate = null)
        {
            try
            {
                _logger.LogInformation("ExportJournalsToPdfAsync: Starting for UserId: {UserId}", userId);
                
                var entries = await GetEntriesByDateRangeAsync(userId, startDate, endDate);
                _logger.LogInformation("ExportJournalsToPdfAsync: Got {Count} entries", entries.Count);
                
                if (entries.Count == 0)
                {
                    _logger.LogWarning("ExportJournalsToPdfAsync: No entries to export for UserId: {UserId}", userId);
                    return Array.Empty<byte>();
                }

                var document = CreateJournalDocument(entries, startDate, endDate);

                using var memoryStream = new MemoryStream();
                document.Save(memoryStream);
                
                _logger.LogInformation("ExportJournalsToPdfAsync: Document saved successfully for UserId: {UserId}, Size: {Size} bytes", userId, memoryStream.Length);
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExportJournalsToPdfAsync for UserId: {UserId}", userId);
                throw;
            }
        }

        public async Task ExportAndSaveJournalsAsync(int userId, DateOnly? startDate = null, DateOnly? endDate = null)
        {
            try
            {
                _logger.LogInformation("ExportAndSaveJournalsAsync: Starting for UserId: {UserId}", userId);

                var pdfBytes = await ExportJournalsToPdfAsync(userId, startDate, endDate);
                var fileName = GenerateFileName(startDate, endDate);

                _logger.LogInformation("PDF generated. Size: {Size} bytes, File: {FileName}", pdfBytes.Length, fileName);

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    throw new Exception("Generated PDF data is empty");
                }

                var cacheDir = FileSystem.Current.CacheDirectory;
                var fullPath = Path.Combine(cacheDir, fileName);
                
                var directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                await File.WriteAllBytesAsync(fullPath, pdfBytes);
                
                _logger.LogInformation("PDF saved successfully to: {FullPath}", fullPath);
                
                try
                {
                    await Launcher.Default.OpenAsync(fullPath);
                    _logger.LogInformation("Launcher opened PDF successfully: {FullPath}", fullPath);
                }
                catch (Exception launchEx)
                {
                    _logger.LogWarning(launchEx, "Failed to open PDF with Launcher: {FullPath}", fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExportAndSaveJournalsAsync for UserId: {UserId}", userId);
                throw;
            }
        }

        private async Task<List<JournalEntry>> GetEntriesByDateRangeAsync(int userId, DateOnly? startDate, DateOnly? endDate)
        {
            try
            {
                var query = _context.JournalEntries
                    .Include(e => e.JournalEntryMoods)
                    .ThenInclude(jem => jem.Mood)
                    .Where(e => e.UserId == userId);

                if (startDate.HasValue)
                    query = query.Where(e => e.EntryDate >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(e => e.EntryDate <= endDate.Value);

                return await query
                    .OrderByDescending(e => e.EntryDate)
                    .ThenByDescending(e => e.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetEntriesByDateRangeAsync for UserId: {UserId}", userId);
                throw;
            }
        }

        private PdfDocument CreateJournalDocument(List<JournalEntry> entries, DateOnly? startDate, DateOnly? endDate)
        {
            var document = new PdfDocument();
            document.Info.Title = "MindLog Journal Entries";
            document.Info.Subject = "Journal Export";
            document.Info.Author = "MindLog";

            var page = document.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;
            var graphics = XGraphics.FromPdfPage(page);

            var layout = new PdfLayout(graphics);
            var yPosition = DrawDocumentHeader(graphics, layout, startDate, endDate);

            if (!entries.Any())
            {
                DrawNoEntriesMessage(graphics, layout);
                graphics.Dispose();
                return document;
            }

            foreach (var entry in entries)
            {
                if (yPosition > layout.PageBottomThreshold)
                {
                    DrawFooter(graphics, document.PageCount, layout);
                    graphics.Dispose();
                    page = document.AddPage();
                    graphics = XGraphics.FromPdfPage(page);
                    yPosition = layout.TopMargin;
                }

                yPosition = DrawJournalEntry(graphics, layout, entry, yPosition, document, page);
            }

            DrawFooter(graphics, document.PageCount, layout);
            graphics.Dispose();
            return document;
        }

        private double DrawDocumentHeader(XGraphics graphics, PdfLayout layout, DateOnly? startDate, DateOnly? endDate)
        {
            var yPosition = layout.TopMargin;
            var titleText = "MindLog Journal Export";
            var titleSize = graphics.MeasureString(titleText, layout.TitleFont);
            graphics.DrawString(titleText, layout.TitleFont, layout.TitleBrush,
                layout.LeftMargin + (layout.ContentWidth - titleSize.Width) / 2, yPosition);
            yPosition += 35;

            var dateRangeText = GetDateRangeText(startDate, endDate);
            var dateSize = graphics.MeasureString(dateRangeText, layout.DateFont);
            graphics.DrawString(dateRangeText, layout.DateFont, layout.GrayBrush,
                layout.LeftMargin + (layout.ContentWidth - dateSize.Width) / 2, yPosition);
            yPosition += 30;

            graphics.DrawLine(new XPen(XColors.LightGray, 1), layout.LeftMargin, yPosition, layout.PageWidth - layout.RightMargin, yPosition);
            return yPosition + 20;
        }

        private string GetDateRangeText(DateOnly? startDate, DateOnly? endDate) => (startDate, endDate) switch
        {
            (not null, not null) => $"Date Range: {startDate:MMMM d, yyyy} - {endDate:MMMM d, yyyy}",
            (not null, null) => $"From: {startDate:MMMM d, yyyy}",
            (null, not null) => $"Until: {endDate:MMMM d, yyyy}",
            _ => "All Entries"
        };

        private void DrawNoEntriesMessage(XGraphics graphics, PdfLayout layout)
        {
            var noText = "No journal entries found for the selected date range.";
            var noSize = graphics.MeasureString(noText, layout.NormalFont);
            graphics.DrawString(noText, layout.NormalFont, layout.TextBrush,
                layout.LeftMargin + (layout.ContentWidth - noSize.Width) / 2, layout.TopMargin + 20);
        }

        private double DrawJournalEntry(XGraphics graphics, PdfLayout layout, JournalEntry entry, double yPosition, PdfDocument document, PdfPage page)
        {
            var entryTop = yPosition;

            graphics.DrawString(entry.Title, layout.HeaderFont, layout.TitleBrush, layout.LeftMargin, yPosition);
            yPosition += graphics.MeasureString(entry.Title, layout.HeaderFont).Height + 8;

            yPosition = DrawEntryMetadata(graphics, layout, entry, yPosition);
            yPosition += 5;

            yPosition = DrawEntryContent(graphics, layout, entry, yPosition, document, page);
            yPosition = DrawEntryMoods(graphics, layout, entry, yPosition);

            var entryHeight = yPosition - entryTop;
            graphics.DrawRectangle(new XPen(XColors.LightGray, 1), layout.LeftMargin, entryTop, layout.ContentWidth, entryHeight);

            return yPosition + 20;
        }

        private double DrawEntryMetadata(XGraphics graphics, PdfLayout layout, JournalEntry entry, double yPosition)
        {
            var dateText = $"Date: {entry.EntryDate:MMMM d, yyyy}";
            if (entry.UpdatedAt > entry.CreatedAt)
                dateText += $"  |  Updated: {entry.UpdatedAt:MMMM d, yyyy}";
            graphics.DrawString(dateText, layout.DateFont, layout.GrayBrush, layout.LeftMargin, yPosition);
            yPosition += graphics.MeasureString(dateText, layout.DateFont).Height + 8;

            if (!string.IsNullOrEmpty(entry.Tags))
            {
                var tagsText = $"Tags: {entry.Tags}";
                graphics.DrawString(tagsText, layout.DateFont, layout.GrayBrush, layout.LeftMargin, yPosition);
                yPosition += graphics.MeasureString(tagsText, layout.DateFont).Height + 8;
            }

            return yPosition;
        }

        private double DrawEntryContent(XGraphics graphics, PdfLayout layout, JournalEntry entry, double yPosition, PdfDocument document, PdfPage page)
        {
            var contentLines = WordWrap(entry.Content, layout.NormalFont, layout.ContentWidth, graphics);
            foreach (var line in contentLines)
            {
                if (yPosition > layout.PageBottomThreshold)
                {
                    DrawFooter(graphics, document.PageCount, layout);
                    graphics.Dispose();
                    page = document.AddPage();
                    graphics = XGraphics.FromPdfPage(page);
                    yPosition = layout.TopMargin;
                }
                graphics.DrawString(line, layout.NormalFont, layout.TextBrush, layout.LeftMargin, yPosition);
                yPosition += 15;
            }
            return yPosition;
        }

        private double DrawEntryMoods(XGraphics graphics, PdfLayout layout, JournalEntry entry, double yPosition)
        {
            if (!entry.JournalEntryMoods.Any())
                return yPosition;

            yPosition += 5;
            var moods = entry.JournalEntryMoods.ToList();
            var primary = moods.FirstOrDefault(m => m.IsPrimary);
            if (primary == null)
                return yPosition;

            var moodText = $"{primary.Mood.Icon} {primary.Mood.Name} (Primary)";
            graphics.DrawString("Moods: ", layout.DateFont, layout.GrayBrush, layout.LeftMargin, yPosition);
            var labelSize = graphics.MeasureString("Moods: ", layout.DateFont);
            graphics.DrawString(moodText, layout.DateFont, layout.GrayBrush, layout.LeftMargin + labelSize.Width, yPosition);
            yPosition += 15;

            var secondary = moods.Where(m => !m.IsPrimary);
            if (secondary.Any())
            {
                var secText = string.Join(", ", secondary.Select(m => $"{m.Mood.Icon} {m.Mood.Name}"));
                graphics.DrawString(secText, layout.DateFont, layout.GrayBrush, layout.LeftMargin + labelSize.Width, yPosition);
                yPosition += 15;
            }

            return yPosition;
        }

        private class PdfLayout
        {
            public PdfLayout(XGraphics graphics)
            {
                TitleFont = new XFont("Arial", 20, XFontStyle.Bold);
                HeaderFont = new XFont("Arial", 14, XFontStyle.Bold);
                DateFont = new XFont("Arial", 10, XFontStyle.Italic);
                NormalFont = new XFont("Arial", 11, XFontStyle.Regular);
                FooterFont = new XFont("Arial", 9, XFontStyle.Italic);

                TitleBrush = XBrushes.DarkBlue;
                TextBrush = XBrushes.Black;
                GrayBrush = XBrushes.Gray;
            }

            public double LeftMargin { get; } = 50.0;
            public double RightMargin { get; } = 50.0;
            public double PageWidth { get; } = 595.0;
            public double PageHeight { get; } = 842.0;
            public double TopMargin { get; } = 50.0;
            public double PageBottomThreshold { get; } = 750.0;
            public double ContentWidth => PageWidth - LeftMargin - RightMargin;

            public XFont TitleFont { get; }
            public XFont HeaderFont { get; }
            public XFont DateFont { get; }
            public XFont NormalFont { get; }
            public XFont FooterFont { get; }

            public XBrush TitleBrush { get; }
            public XBrush TextBrush { get; }
            public XBrush GrayBrush { get; }
        }

        private void DrawFooter(XGraphics graphics, int pageNumber, PdfLayout layout)
        {
            var footerText = $"Page {pageNumber}";
            var footerFont = new XFont("Arial", 9, XFontStyle.Italic);
            var footerSize = graphics.MeasureString(footerText, footerFont);
            graphics.DrawString(footerText, footerFont, XBrushes.Gray,
                layout.PageWidth - layout.RightMargin - footerSize.Width, layout.PageHeight - 30);
        }

        private List<string> WordWrap(string text, XFont font, double maxWidth, XGraphics graphics)
        {
            var lines = new List<string>();
            var paragraphs = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var paragraph in paragraphs)
            {
                var words = paragraph.Split(' ');
                var currentLine = "";

                foreach (var word in words)
                {
                    var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                    if (graphics.MeasureString(testLine, font).Width > maxWidth)
                    {
                        if (!string.IsNullOrEmpty(currentLine))
                        {
                            lines.Add(currentLine);
                            currentLine = word;
                        }
                        else
                        {
                            lines.Add(word);
                        }
                    }
                    else
                    {
                        currentLine = testLine;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                    lines.Add(currentLine);

                lines.Add(""); // paragraph spacing
            }

            return lines;
        }

        private string GenerateFileName(DateOnly? startDate, DateOnly? endDate)
        {
            var baseName = "MindLog_Journal_Export";
            return (startDate, endDate) switch
            {
                (not null, not null) => $"{baseName}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.pdf",
                (not null, null) => $"{baseName}_from_{startDate:yyyyMMdd}.pdf",
                (null, not null) => $"{baseName}_until_{endDate:yyyyMMdd}.pdf",
                _ => $"{baseName}_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }
    }

    public class FontResolver : IFontResolver
    {
        private const string FONT_KEY = "MindLogFont";
        private static readonly ILogger _logger = Logger.GetLogger<FontResolver>();

        public string DefaultFontName => FONT_KEY;

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            return new FontResolverInfo(FONT_KEY);
        }

        public byte[] GetFont(string faceName)
        {
            try
            {
                var assembly = typeof(FontResolver).Assembly;
                var resourceNames = assembly.GetManifestResourceNames();

                string[] possibleFontNames = new[]
                {
                    "OpenSans-Regular.ttf",
                    "Fonts.OpenSans-Regular.ttf",
                    "Resources.OpenSans-Regular.ttf",
                    "mindlog.OpenSans-Regular.ttf"
                };

                string? fullResourceName = null;
                foreach (var fontName in possibleFontNames)
                {
                    fullResourceName = resourceNames.FirstOrDefault(x => x.EndsWith(fontName, StringComparison.OrdinalIgnoreCase));
                    if (fullResourceName != null)
                    {
                        _logger.LogDebug("Found font: {FontName}", fullResourceName);
                        break;
                    }
                }

                if (string.IsNullOrEmpty(fullResourceName))
                {
                    _logger.LogWarning("Font not found. Available resources: {Resources}", string.Join(", ", resourceNames));
                    return GetFallbackFont();
                }

                _logger.LogDebug("Loading font from resource: {ResourceName}", fullResourceName);

                using var stream = assembly.GetManifestResourceStream(fullResourceName);
                if (stream == null)
                {
                    _logger.LogWarning("Stream is null for resource: {ResourceName}", fullResourceName);
                    return GetFallbackFont();
                }

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading font");
                return GetFallbackFont();
            }
        }

        private byte[] GetFallbackFont()
        {
            _logger.LogDebug("Using fallback font (Arial)");
            return Array.Empty<byte>();
        }
    }
}