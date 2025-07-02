using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Mimsv2.Models;

public class ExportIncidentTrendPdfService
{
    public byte[] Generate(List<IncidentTrendSummaryModel> data, int year)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Size(PageSizes.A4);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Text($"Incident Trend Summary - {year}").Bold().FontSize(14).AlignCenter();
                page.Content().Table(table =>
                {
                    // Columns
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(50); // PTE
                        columns.ConstantColumn(50); // Type
                        columns.ConstantColumn(50); // Cat1
                        columns.ConstantColumn(50); // Cat2
                        for (int i = 0; i < 16; i++)
                            columns.ConstantColumn(20); // Jan-Dec, Q1-Q4
                        columns.ConstantColumn(30); // Total
                        columns.ConstantColumn(30); // Goal
                    });

                    // Header row
                    table.Header(header =>
                    {
                        header.Cell().Text("PTE").Bold();
                        header.Cell().Text("Type").Bold();
                        header.Cell().Text("Cat 1").Bold();
                        header.Cell().Text("Cat 2").Bold();
                        header.Cell().Text("Jan"); header.Cell().Text("Feb"); header.Cell().Text("Mar"); header.Cell().Text("Q1");
                        header.Cell().Text("Apr"); header.Cell().Text("May"); header.Cell().Text("Jun"); header.Cell().Text("Q2");
                        header.Cell().Text("Jul"); header.Cell().Text("Aug"); header.Cell().Text("Sep"); header.Cell().Text("Q3");
                        header.Cell().Text("Oct"); header.Cell().Text("Nov"); header.Cell().Text("Dec"); header.Cell().Text("Q4");
                        header.Cell().Text("Total").Bold();
                        header.Cell().Text("Goal").Bold();
                    });

                    foreach (var item in data)
                    {
                        table.Cell().Text(item.PTE);
                        table.Cell().Text(item.IncidentType);
                        table.Cell().Text(item.IncTypesCat1);
                        table.Cell().Text(item.IncTypesCat2);

                        table.Cell().Text(item.Jan.ToString());
                        table.Cell().Text(item.Feb.ToString());
                        table.Cell().Text(item.Mar.ToString());
                        table.Cell().Text(item.Q1.ToString());

                        table.Cell().Text(item.Apr.ToString());
                        table.Cell().Text(item.May.ToString());
                        table.Cell().Text(item.Jun.ToString());
                        table.Cell().Text(item.Q2.ToString());

                        table.Cell().Text(item.Jul.ToString());
                        table.Cell().Text(item.Aug.ToString());
                        table.Cell().Text(item.Sep.ToString());
                        table.Cell().Text(item.Q3.ToString());

                        table.Cell().Text(item.Oct.ToString());
                        table.Cell().Text(item.Nov.ToString());
                        table.Cell().Text(item.Dec.ToString());
                        table.Cell().Text(item.Q4.ToString());

                        table.Cell().Text(item.Total.ToString()).Bold();
                        table.Cell().Text(item.Goal.ToString()).FontColor(Colors.Blue.Medium);
                    }
                });

                page.Footer().AlignCenter().Text($"Generated on {DateTime.Now:yyyy-MM-dd HH:mm}");
            });
        });

        return document.GeneratePdf();
    }
}
