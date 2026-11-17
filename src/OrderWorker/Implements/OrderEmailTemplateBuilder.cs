using System.Net;
using System.Text;

namespace OrderWorker.Implements;

public static class OrderEmailTemplateBuilder
{
    public static string BuildSubject(OrderCreatedMessage order) =>
        $"تأكيد استلام طلبك رقم #{order.OrderId}";

    public static string BuildPlainText(OrderCreatedMessage order)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"مرحباً {order.CustomerName}،");
        sb.AppendLine();
        sb.AppendLine("شكراً لتسوقك معنا! تم استلام طلبك بنجاح وفيما يلي تفاصيل الطلب:");
        sb.AppendLine($"- رقم الطلب: {order.OrderId}");
        sb.AppendLine($"- تاريخ الطلب (UTC): {order.CreatedAtUtc:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("المنتجات المطلوبة:");

        if (order.Items is { Count: > 0 })
        {
            foreach (var item in order.Items)
            {
                var lineTotal = item.UnitPrice * item.Quantity;
                sb.AppendLine($"* {item.ProductName} | الكمية: {item.Quantity} | سعر الوحدة: {item.UnitPrice:F2} | المجموع: {lineTotal:F2}");
            }
        }
        else
        {
            sb.AppendLine("* (لا توجد عناصر مسجلة)");
        }

        sb.AppendLine();
        sb.AppendLine($"الإجمالي الكلي: {order.Total:F2}");
        sb.AppendLine();
        sb.AppendLine("مع خالص التحية،");
        sb.AppendLine("فريق المتجر الإلكتروني");

        return sb.ToString();
    }

    public static string BuildHtml(OrderCreatedMessage order)
    {
        var rowsBuilder = new StringBuilder();

        if (order.Items is { Count: > 0 })
        {
            foreach (var item in order.Items)
            {
                var lineTotal = item.UnitPrice * item.Quantity;
                var safeProductName = WebUtility.HtmlEncode(item.ProductName);

                rowsBuilder.AppendLine($"""
                    <tr>
                        <td style="padding: 8px; border: 1px solid #ddd;">{safeProductName}</td>
                        <td style="padding: 8px; border: 1px solid #ddd; text-align: center;">{item.Quantity}</td>
                        <td style="padding: 8px; border: 1px solid #ddd; text-align: center;">{item.UnitPrice:F2}</td>
                        <td style="padding: 8px; border: 1px solid #ddd; text-align: center;">{lineTotal:F2}</td>
                    </tr>
                    """);
            }
        }
        else
        {
            rowsBuilder.AppendLine("""
                <tr>
                    <td colspan="4" style="padding: 8px; border: 1px solid #ddd; text-align: center;">لا توجد منتجات</td>
                </tr>
                """);
        }

        var safeCustomerName = WebUtility.HtmlEncode(order.CustomerName);

        return $$"""
            <!DOCTYPE html>
            <html lang="ar" dir="rtl">
            <head>
                <meta charset="UTF-8" />
                <title>تأكيد الطلب</title>
            </head>
            <body style="font-family: Tahoma, Arial, sans-serif; direction: rtl; text-align: right; color: #222; line-height: 1.6;">
                <div style="max-width: 600px; margin: 0 auto; border: 1px solid #e5e7eb; border-radius: 8px; padding: 20px;">
                    <h2 style="color: #1d4ed8; margin-top: 0;">مرحباً {{safeCustomerName}}،</h2>
                    <p>شكراً لتسوقك معنا! تم استلام طلبك بنجاح وتجري معالجته الآن.</p>

                    <div style="background-color: #f9fafb; padding: 12px; border-radius: 6px; margin-bottom: 16px;">
                        <p style="margin: 4px 0;"><strong>رقم الطلب:</strong> {{order.OrderId}}</p>
                        <p style="margin: 4px 0;"><strong>تاريخ الطلب (UTC):</strong> {{order.CreatedAtUtc:yyyy-MM-dd HH:mm:ss}}</p>
                    </div>

                    <h3 style="margin-bottom: 8px;">تفاصيل المنتجات:</h3>
                    <table style="width: 100%; border-collapse: collapse; margin-bottom: 16px;">
                        <thead>
                            <tr style="background-color: #f3f4f6;">
                                <th style="padding: 8px; border: 1px solid #ddd; text-align: right;">المنتج</th>
                                <th style="padding: 8px; border: 1px solid #ddd; text-align: center;">الكمية</th>
                                <th style="padding: 8px; border: 1px solid #ddd; text-align: center;">سعر الوحدة</th>
                                <th style="padding: 8px; border: 1px solid #ddd; text-align: center;">الإجمالي الفرعي</th>
                            </tr>
                        </thead>
                        <tbody>
                            {{rowsBuilder}}
                        </tbody>
                    </table>

                    <p style="font-size: 18px; font-weight: bold; color: #111827;">
                        الإجمالي الكلي: {{order.Total:F2}}
                    </p>

                    <hr style="border: none; border-top: 1px solid #e5e7eb; margin: 20px 0;" />
                    <p style="font-size: 13px; color: #6b7280; margin-bottom: 0;">مع خالص التحية،<br />فريق المتجر الإلكتروني</p>
                </div>
            </body>
            </html>
            """;
    }
}