using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using MindLog.Data;
using MindLog.Models;
using Microsoft.Maui.Storage;

namespace MindLog.Services
{
    public class PdfExportService
    {
        private readonly AppDbContext _context;

        static PdfExportService()
        {
            try
            {
                GlobalFontSettings.FontResolver = new FontResolver();
                System.Diagnostics.Debug.WriteLine("Font resolver initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing font resolver: {ex.Message}");
            }
        }

        public PdfExportService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<byte[]> ExportJournalsToPdfAsync(int userId, DateOnly? startDate = null, DateOnly? endDate = null)
        {
            System.Diagnostics.Debug.WriteLine("ExportJournalsToPdfAsync: Starting...");
            
            try
            {
                System.Diagnostics.Debug.WriteLine("ExportJournalsToPdfAsync: Fetching entries...");
                var entries = await GetEntriesByDateRangeAsync(userId, startDate, endDate);
                System.Diagnostics.Debug.WriteLine($"ExportJournalsToPdfAsync: Got {entries.Count} entries");
                
                if (entries.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("ExportJournalsToPdfAsync: No entries to export");
                    return Array.Empty<byte>();
                }

                System.Diagnostics.Debug.WriteLine("ExportJournalsToPdfAsync: Creating document...");
                var document = CreateJournalDocument(entries, startDate, endDate);
                System.Diagnostics.Debug.WriteLine("ExportJournalsToPdfAsync: Document created");

                using var memoryStream = new MemoryStream();
                System.Diagnostics.Debug.WriteLine("ExportJournalsToPdfAsync: Saving to stream...");
                document.Save(memoryStream);
                System.Diagnostics.Debug.WriteLine("ExportJournalsToPdfAsync: Saved to stream");
                
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ExportJournalsToPdfAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task ExportAndSaveJournalsAsync(int userId, DateOnly? startDate = null, DateOnly? endDate = null)
        {
            System.Diagnostics.Debug.WriteLine("=== Starting PDF Export ===");

            try
            {
                var pdfBytes = await ExportJournalsToPdfAsync(userId, startDate, endDate);
                var fileName = GenerateFileName(startDate, endDate);

                System.Diagnostics.Debug.WriteLine($"PDF generated. Size: {pdfBytes.Length} bytes, File: {fileName}");

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("PDF data is empty, cannot save");
                    throw new Exception("Generated PDF data is empty");
                }

                System.Diagnostics.Debug.WriteLine("Saving to app cache directory...");
                
                var cacheDir = FileSystem.Current.CacheDirectory;
                var fullPath = Path.Combine(cacheDir, fileName);
                
                System.Diagnostics.Debug.WriteLine($"Saving to: {fullPath}");
                
                var directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                await File.WriteAllBytesAsync(fullPath, pdfBytes);
                
                System.Diagnostics.Debug.WriteLine($"PDF saved successfully to: {fullPath}");
                
                try
                {
                    System.Diagnostics.Debug.WriteLine("Attempting to open PDF with Launcher...");
                    await Launcher.Default.OpenAsync(fullPath);
                    System.Diagnostics.Debug.WriteLine("Launcher opened PDF successfully");
                }
                catch (Exception launchEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to open PDF with Launcher: {launchEx.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ExportAndSaveJournalsAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }

            System.Diagnostics.Debug.WriteLine("=== ExportAndSaveJournalsAsync completed ===");
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
                System.Diagnostics.Debug.WriteLine($"Error in GetEntriesByDateRangeAsync: {ex.Message}");
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

            var yPosition = 50.0;
            const double leftMargin = 50.0;
            const double rightMargin = 50.0;
            const double pageWidth = 595.0;
            const double contentWidth = pageWidth - leftMargin - rightMargin;

            var titleFont = new XFont("Arial", 20, XFontStyle.Bold);
            var headerFont = new XFont("Arial", 14, XFontStyle.Bold);
            var dateFont = new XFont("Arial", 10, XFontStyle.Italic);
            var normalFont = new XFont("Arial", 11, XFontStyle.Regular);
            var footerFont = new XFont("Arial", 9, XFontStyle.Italic);

            var titleBrush = XBrushes.DarkBlue;
            var textBrush = XBrushes.Black;
            var grayBrush = XBrushes.Gray;

            // Title
            var titleText = "MindLog Journal Export";
            var titleSize = graphics.MeasureString(titleText, titleFont);
            graphics.DrawString(titleText, titleFont, titleBrush,
                leftMargin + (contentWidth - titleSize.Width) / 2, yPosition);
            yPosition += 35;

            // Date range
            string dateRangeText = (startDate, endDate) switch
            {
                (not null, not null) => $"Date Range: {startDate:MMMM d, yyyy} - {endDate:MMMM d, yyyy}",
                (not null, null) => $"From: {startDate:MMMM d, yyyy}",
                (null, not null) => $"Until: {endDate:MMMM d, yyyy}",
                _ => "All Entries"
            };

            var dateSize = graphics.MeasureString(dateRangeText, dateFont);
            graphics.DrawString(dateRangeText, dateFont, grayBrush,
                leftMargin + (contentWidth - dateSize.Width) / 2, yPosition);
            yPosition += 30;

            graphics.DrawLine(new XPen(XColors.LightGray, 1), leftMargin, yPosition, pageWidth - rightMargin, yPosition);
            yPosition += 20;

            if (!entries.Any())
            {
                var noText = "No journal entries found for the selected date range.";
                var noSize = graphics.MeasureString(noText, normalFont);
                graphics.DrawString(noText, normalFont, textBrush,
                    leftMargin + (contentWidth - noSize.Width) / 2, yPosition + 20);
                graphics.Dispose();
                return document;
            }

            foreach (var entry in entries)
            {
                if (yPosition > 750)
                {
                    DrawFooter(graphics, document.PageCount, page.Height);
                    graphics.Dispose();
                    page = document.AddPage();
                    graphics = XGraphics.FromPdfPage(page);
                    yPosition = 50;
                }

                var entryTop = yPosition;

                // Title
                graphics.DrawString(entry.Title, headerFont, titleBrush, leftMargin, yPosition);
                yPosition += graphics.MeasureString(entry.Title, headerFont).Height + 8;

                // Date + Updated
                var dateText = $"Date: {entry.EntryDate:MMMM d, yyyy}";
                if (entry.UpdatedAt > entry.CreatedAt)
                    dateText += $"  |  Updated: {entry.UpdatedAt:MMMM d, yyyy}";
                graphics.DrawString(dateText, dateFont, grayBrush, leftMargin, yPosition);
                yPosition += graphics.MeasureString(dateText, dateFont).Height + 8;

                // Tags
                if (!string.IsNullOrEmpty(entry.Tags))
                {
                    var tagsText = $"Tags: {entry.Tags}";
                    graphics.DrawString(tagsText, dateFont, grayBrush, leftMargin, yPosition);
                    yPosition += graphics.MeasureString(tagsText, dateFont).Height + 8;
                }

                yPosition += 5;

                // Content with word wrap
                var contentLines = WordWrap(entry.Content, normalFont, contentWidth, graphics);
                foreach (var line in contentLines)
                {
                    if (yPosition > 750)
                    {
                        DrawFooter(graphics, document.PageCount, page.Height);
                        graphics.Dispose();
                        page = document.AddPage();
                        graphics = XGraphics.FromPdfPage(page);
                        yPosition = 50;
                    }
                    graphics.DrawString(line, normalFont, textBrush, leftMargin, yPosition);
                    yPosition += 15;
                }

                // Moods
                if (entry.JournalEntryMoods.Any())
                {
                    yPosition += 5;
                    var moods = entry.JournalEntryMoods.ToList();
                    var primary = moods.FirstOrDefault(m => m.IsPrimary);
                    if (primary != null)
                    {
                        var moodText = $"{primary.Mood.Icon} {primary.Mood.Name} (Primary)";
                        graphics.DrawString("Moods: ", dateFont, grayBrush, leftMargin, yPosition);
                        var labelSize = graphics.MeasureString("Moods: ", dateFont);
                        graphics.DrawString(moodText, dateFont, grayBrush, leftMargin + labelSize.Width, yPosition);
                        yPosition += 15;

                        var secondary = moods.Where(m => !m.IsPrimary);
                        if (secondary.Any())
                        {
                            var secText = string.Join(", ", secondary.Select(m => $"{m.Mood.Icon} {m.Mood.Name}"));
                            graphics.DrawString(secText, dateFont, grayBrush, leftMargin + labelSize.Width, yPosition);
                            yPosition += 15;
                        }
                    }
                }

                // Entry border
                var entryHeight = yPosition - entryTop;
                graphics.DrawRectangle(new XPen(XColors.LightGray, 1), leftMargin, entryTop, contentWidth, entryHeight);

                yPosition += 20;
            }

            // Final footer
            DrawFooter(graphics, document.PageCount, page.Height);
            graphics.Dispose();
            return document;
        }

        private void DrawFooter(XGraphics graphics, int pageNumber, double pageHeight)
        {
            var footerText = $"Page {pageNumber}";
            var footerFont = new XFont("Arial", 9, XFontStyle.Italic);
            var footerSize = graphics.MeasureString(footerText, footerFont);
            graphics.DrawString(footerText, footerFont, XBrushes.Gray,
                595 - 50 - footerSize.Width, pageHeight - 30);
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

                // Try to find the font with different possible names
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
                        System.Diagnostics.Debug.WriteLine($"Found font: {fullResourceName}");
                        break;
                    }
                }

                if (string.IsNullOrEmpty(fullResourceName))
                {
                    System.Diagnostics.Debug.WriteLine($"Font not found. Available resources: {string.Join(", ", resourceNames)}");
                    // Fallback to Arial which should be available on most systems
                    return GetFallbackFont();
                }

                System.Diagnostics.Debug.WriteLine($"Loading font from resource: {fullResourceName}");

                using var stream = assembly.GetManifestResourceStream(fullResourceName);
                if (stream == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Stream is null for resource: {fullResourceName}");
                    return GetFallbackFont();
                }

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading font: {ex.Message}");
                return GetFallbackFont();
            }
        }

        private byte[] GetFallbackFont()
        {
            System.Diagnostics.Debug.WriteLine("Using fallback font (Arial)");
            return Array.Empty<byte>();
        }
    }
}