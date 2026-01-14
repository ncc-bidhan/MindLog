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
                
                var user = await _context.Users.FindAsync(userId);
                var userName = user?.Username ?? "MindLog User";
                
                var entries = await GetEntriesByDateRangeAsync(userId, startDate, endDate);
                _logger.LogInformation("ExportJournalsToPdfAsync: Got {Count} entries", entries.Count);
                
                if (entries.Count == 0)
                {
                    _logger.LogWarning("ExportJournalsToPdfAsync: No entries to export for UserId: {UserId}", userId);
                    return Array.Empty<byte>();
                }

                using (var document = CreateJournalDocument(entries, startDate, endDate, userName))
                {
                    using var memoryStream = new MemoryStream();
                    document.Save(memoryStream);

                    _logger.LogInformation("ExportJournalsToPdfAsync: Document saved successfully for UserId: {UserId}, Size: {Size} bytes", userId, memoryStream.Length);
                    return memoryStream.ToArray();
                }
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
                
                // Cleanup old export files to save space
                CleanupOldExports(cacheDir);

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

        private void CleanupOldExports(string cacheDir)
        {
            try
            {
                var directory = new DirectoryInfo(cacheDir);
                if (!directory.Exists) return;

                var oldFiles = directory.GetFiles("MindLog_Journal_Export_*.pdf")
                    .Where(f => f.LastWriteTime < DateTime.Now.AddHours(-1))
                    .ToList();

                foreach (var file in oldFiles)
                {
                    try
                    {
                        file.Delete();
                        _logger.LogInformation("Deleted old export file: {FileName}", file.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete old export file: {FileName}", file.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up old export files");
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

        private class PdfGenerationContext
        {
            public PdfDocument Document { get; }
            public PdfPage CurrentPage { get; set; }
            public XGraphics Graphics { get; set; }
            public double YPosition { get; set; }
            public PdfLayout Layout { get; }
            public bool IsFirstPage { get; set; } = true;

            public PdfGenerationContext(PdfDocument document, PdfLayout layout)
            {
                Document = document;
                Layout = layout;
                AddNewPage();
            }

            public void AddNewPage()
            {
                // Draw footer on the previous page if it's not the first page
                if (!IsFirstPage && Graphics != null)
                {
                    DrawFooter();
                    Graphics.Dispose();
                }
                
                CurrentPage = Document.AddPage();
                CurrentPage.Size = PdfSharpCore.PageSize.A4;
                Graphics = XGraphics.FromPdfPage(CurrentPage);
                YPosition = Layout.TopMargin;
                
                // Reset the first page flag after creating the first page
                if (IsFirstPage)
                {
                    IsFirstPage = false;
                }
            }

            public void DrawFooter()
            {
                var footerText = $"Page {Document.PageCount}";
                var footerSize = Graphics.MeasureString(footerText, Layout.FooterFont);
                Graphics.DrawString(footerText, Layout.FooterFont, XBrushes.Gray,
                    Layout.PageWidth - Layout.RightMargin - footerSize.Width, Layout.PageHeight - 30);
                
                var dateText = DateTime.Now.ToString("MMMM d, yyyy HH:mm");
                Graphics.DrawString($"Exported from MindLog on {dateText}", Layout.FooterFont, XBrushes.LightGray,
                    Layout.LeftMargin, Layout.PageHeight - 30);
            }

            public void EnsureSpace(double height)
            {
                if (YPosition + height > Layout.PageBottomThreshold)
                {
                    AddNewPage();
                }
            }

            public void FinalizeDocument()
            {
                // Draw footer on the last page
                if (Graphics != null)
                {
                    DrawFooter();
                    Graphics.Dispose();
                }
            }
        }

        private PdfDocument CreateJournalDocument(List<JournalEntry> entries, DateOnly? startDate, DateOnly? endDate, string userName)
        {
            var document = new PdfDocument();
            document.Info.Title = $"MindLog Journal - {userName}";
            document.Info.Subject = "Journal Export";
            document.Info.Author = "MindLog";

            var layout = new PdfLayout();
            var context = new PdfGenerationContext(document, layout);
            
            try
            {
                DrawDocumentHeader(context, startDate, endDate, userName);

                if (!entries.Any())
                {
                    DrawNoEntriesMessage(context);
                }
                else
                {
                    foreach (var entry in entries)
                    {
                        DrawJournalEntry(context, entry);
                    }
                }

                // Make sure the last page has a footer
                context.FinalizeDocument();
                
                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating PDF document");
                context.FinalizeDocument();
                throw;
            }
        }

        private void DrawDocumentHeader(PdfGenerationContext context, DateOnly? startDate, DateOnly? endDate, string userName)
        {
            var layout = context.Layout;

            // Draw a nice header background
            var headerRect = new XRect(0, 0, layout.PageWidth, 140);
            var headerBrush = new XLinearGradientBrush(
                new XPoint(0, 0), new XPoint(layout.PageWidth, 140),
                XColor.FromArgb(255, 30, 58, 138), // Dark Blue
                XColor.FromArgb(255, 59, 130, 246) // Blue
            );
            context.Graphics.DrawRectangle(headerBrush, headerRect);

            var yPos = 40.0;
            var titleText = "MindLog Journal Export";
            var titleSize = context.Graphics.MeasureString(titleText, layout.TitleFont);
            context.Graphics.DrawString(titleText, layout.TitleFont, XBrushes.White,
                (layout.PageWidth - titleSize.Width) / 2, yPos);
            
            yPos += 35;
            var userText = $"Journal for {userName}";
            var userSize = context.Graphics.MeasureString(userText, layout.HeaderFont);
            context.Graphics.DrawString(userText, context.Layout.HeaderFont, XBrushes.WhiteSmoke,
                (layout.PageWidth - userSize.Width) / 2, yPos);

            yPos += 30;
            var dateRangeText = GetDateRangeText(startDate, endDate);
            var dateSize = context.Graphics.MeasureString(dateRangeText, layout.DateFont);
            context.Graphics.DrawString(dateRangeText, layout.DateFont, XBrushes.LightGray,
                (layout.PageWidth - dateSize.Width) / 2, yPos);

            context.YPosition = 160;
        }

        private string GetDateRangeText(DateOnly? startDate, DateOnly? endDate) => (startDate, endDate) switch
        {
            (not null, not null) => $"Date Range: {startDate:MMMM d, yyyy} - {endDate:MMMM d, yyyy}",
            (not null, null) => $"From: {startDate:MMMM d, yyyy}",
            (null, not null) => $"Until: {endDate:MMMM d, yyyy}",
            _ => "All Entries"
        };

        private XColor ParseColor(string htmlColor)
        {
            try
            {
                if (string.IsNullOrEmpty(htmlColor) || !htmlColor.StartsWith("#") || htmlColor.Length < 7)
                    return XColor.FromArgb(255, 107, 114, 128); // Default gray

                return XColor.FromArgb(
                    255,
                    Convert.ToInt32(htmlColor.Substring(1, 2), 16),
                    Convert.ToInt32(htmlColor.Substring(3, 2), 16),
                    Convert.ToInt32(htmlColor.Substring(5, 2), 16)
                );
            }
            catch
            {
                return XColor.FromArgb(255, 107, 114, 128);
            }
        }

        private void DrawNoEntriesMessage(PdfGenerationContext context)
        {
            var noText = "No journal entries found for the selected date range.";
            var noSize = context.Graphics.MeasureString(noText, context.Layout.NormalFont);
            context.Graphics.DrawString(noText, context.Layout.NormalFont, context.Layout.TextBrush,
                context.Layout.LeftMargin + (context.Layout.ContentWidth - noSize.Width) / 2, context.YPosition + 40);
        }

        private void DrawJournalEntry(PdfGenerationContext context, JournalEntry entry)
        {
            var layout = context.Layout;
            
            // Calculate the total height needed for this entry
            double headerHeight = 30.0;
            double metadataHeight = 30.0; // Approximate height for date and tags
            double contentHeight = EstimateContentHeight(entry.Content, layout.NormalFont, layout.ContentWidth - 20, context.Graphics);
            double moodsHeight = entry.JournalEntryMoods?.Any() == true ? 25.0 : 0.0;
            double totalHeight = headerHeight + 10 + metadataHeight + contentHeight + moodsHeight + 30; // +30 for spacing and border
            
            // Ensure we have enough space for the entire entry
            context.EnsureSpace(totalHeight);
            
            // Entry Header Background (light gray)
            var headerRect = new XRect(layout.LeftMargin, context.YPosition, layout.ContentWidth, headerHeight);
            context.Graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(243, 244, 246)), headerRect);
            
            // Title
            context.Graphics.DrawString(entry.Title, layout.HeaderFont, layout.TitleBrush, layout.LeftMargin + 10, context.YPosition + 20);
            context.YPosition += headerHeight + 10;

            // Metadata (Date, Tags)
            context.YPosition = DrawEntryMetadata(context, entry);
            
            // Content
            context.YPosition = DrawEntryContent(context, entry);
            
            // Moods
            context.YPosition = DrawEntryMoods(context, entry);

            // Draw card border
            // If it spanned multiple pages, we draw the bottom line on the current page.
            context.Graphics.DrawLine(new XPen(XColor.FromArgb(229, 231, 235), 1), layout.LeftMargin, context.YPosition, layout.PageWidth - layout.RightMargin, context.YPosition);
            
            context.YPosition += 30; // Space between entries
        }

        private double EstimateContentHeight(string content, XFont font, double maxWidth, XGraphics graphics)
        {
            if (string.IsNullOrEmpty(content)) return 20.0;
            
            var lines = WordWrap(content, font, maxWidth, graphics);
            return lines.Count * 15.0; // 15 points per line
        }

        private double DrawEntryMetadata(PdfGenerationContext context, JournalEntry entry)
        {
            var layout = context.Layout;
            var yPos = context.YPosition;

            // Remove emojis if they cause rendering issues, or use text equivalents
            var dateText = $"Date: {entry.EntryDate:MMMM d, yyyy}";
            if (entry.UpdatedAt > entry.CreatedAt)
                dateText += $"  |  Updated: {entry.UpdatedAt:MMMM d, yyyy}";
            
            context.Graphics.DrawString(dateText, layout.DateFont, layout.GrayBrush, layout.LeftMargin + 10, yPos);
            yPos += 15;

            if (!string.IsNullOrEmpty(entry.Tags))
            {
                var tagsText = $"Tags: {entry.Tags}";
                context.Graphics.DrawString(tagsText, layout.DateFont, layout.GrayBrush, layout.LeftMargin + 10, yPos);
                yPos += 15;
            }

            return yPos + 5;
        }

        private double DrawEntryContent(PdfGenerationContext context, JournalEntry entry)
        {
            var layout = context.Layout;
            var contentLines = WordWrap(entry.Content, layout.NormalFont, layout.ContentWidth - 20, context.Graphics);
            
            foreach (var line in contentLines)
            {
                // Check if we need a new page for this line
                context.EnsureSpace(20);
                
                if (string.IsNullOrEmpty(line))
                {
                    context.YPosition += 10;
                    continue;
                }
                context.Graphics.DrawString(line, layout.NormalFont, layout.TextBrush, layout.LeftMargin + 10, context.YPosition);
                context.YPosition += 15;
            }
            return context.YPosition;
        }

        private double DrawEntryMoods(PdfGenerationContext context, JournalEntry entry)
        {
            if (entry.JournalEntryMoods == null || !entry.JournalEntryMoods.Any())
                return context.YPosition;

            context.EnsureSpace(40);
            var layout = context.Layout;
            var yPos = context.YPosition + 5;

            var moods = entry.JournalEntryMoods.ToList();
            var primary = moods.FirstOrDefault(m => m.IsPrimary);
            
            context.Graphics.DrawString("Moods: ", layout.DateFont, layout.GrayBrush, layout.LeftMargin + 10, yPos);
            var labelSize = context.Graphics.MeasureString("Moods: ", layout.DateFont);
            var xPos = layout.LeftMargin + 10 + labelSize.Width;

            if (primary != null)
            {
                // Use mood name only if icons are causing issues
                var moodText = primary.Mood.Name;
                var moodColor = ParseColor(primary.Mood.Color);
                context.Graphics.DrawString(moodText, layout.DateFont, new XSolidBrush(moodColor), xPos, yPos);
                xPos += context.Graphics.MeasureString(moodText, layout.DateFont).Width + 10;
            }

            var secondary = moods.Where(m => !m.IsPrimary).ToList();
            if (secondary.Any())
            {
                foreach (var m in secondary)
                {
                    var moodText = m.Mood.Name;
                    var moodColor = ParseColor(m.Mood.Color);
                    context.Graphics.DrawString(moodText, layout.DateFont, new XSolidBrush(moodColor), xPos, yPos);
                    xPos += context.Graphics.MeasureString(moodText, layout.DateFont).Width + 8;
                }
            }

            return yPos + 20;
        }

        private class PdfLayout
        {
            public PdfLayout()
            {
                // Use standard fonts as fallback for emojis if possible, 
                // but PdfSharpCore needs specific font loading for symbols.
                TitleFont = new XFont("Arial", 24, XFontStyle.Bold);
                HeaderFont = new XFont("Arial", 16, XFontStyle.Bold);
                DateFont = new XFont("Arial", 10, XFontStyle.Regular);
                NormalFont = new XFont("Arial", 12, XFontStyle.Regular);
                FooterFont = new XFont("Arial", 9, XFontStyle.Italic);

                TitleBrush = new XSolidBrush(XColor.FromArgb(255, 30, 58, 138)); // Dark Blue
                TextBrush = XBrushes.Black;
                GrayBrush = new XSolidBrush(XColor.FromArgb(255, 75, 85, 99)); // Cool Gray
            }

            public double LeftMargin { get; } = 40.0;
            public double RightMargin { get; } = 40.0;
            public double PageWidth { get; } = 595.0;
            public double PageHeight { get; } = 842.0;
            public double TopMargin { get; } = 40.0;
            public double PageBottomThreshold { get; } = 780.0;
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
            // This is now handled in PdfGenerationContext
        }

        private List<string> WordWrap(string text, XFont font, double maxWidth, XGraphics graphics)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();
            
            var lines = new List<string>();
            var paragraphs = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    lines.Add("");
                    continue;
                }

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
                            // Single word is too long, force split it
                            lines.Add(word);
                            currentLine = "";
                        }
                    }
                    else
                    {
                        currentLine = testLine;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                    lines.Add(currentLine);
            }

            return lines;
        }

        private string GenerateFileName(DateOnly? startDate, DateOnly? endDate)
        {
            var baseName = "MindLog_Journal_Export";
            var timestamp = DateTime.Now.ToString("HHmmss");
            return (startDate, endDate) switch
            {
                (not null, not null) => $"{baseName}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_{timestamp}.pdf",
                (not null, null) => $"{baseName}_from_{startDate:yyyyMMdd}_{timestamp}.pdf",
                (null, not null) => $"{baseName}_until_{endDate:yyyyMMdd}_{timestamp}.pdf",
                _ => $"{baseName}_{DateTime.Now:yyyyMMdd}_{timestamp}.pdf"
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