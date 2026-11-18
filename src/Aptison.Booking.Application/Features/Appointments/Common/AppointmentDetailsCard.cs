using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Application.Messaging;

namespace Aptison.Booking.Application.Features.Appointments.Common;

/// <summary>
/// The appointment as the customer's account page shows it: the shop, the date and time, then a
/// caption and a value per row. A phone stacks each caption above its value.
/// </summary>
internal static partial class AppointmentDetailsCard
{
    /// <param name="cancelled">Strikes the date and time through.</param>
    /// <param name="reason">Plain text. The first row when present.</param>
    public static string Build(
        AppointmentSummary summary,
        CultureInfo culture,
        bool greek,
        string? appUrl,
        bool cancelled = false,
        string? reason = null)
    {
        var labels = Labels.Create(greek);
        var date = AppointmentEmailFormats.FormatDate(summary.StartsAt, summary.Timezone, culture);
        var start = AppointmentEmailFormats.FormatTime(summary.StartsAt, summary.Timezone, culture, greek);
        var end = AppointmentEmailFormats.FormatTime(
            summary.StartsAt.AddMinutes(summary.DurationMinutes), summary.Timezone, culture, greek);
        var strike = cancelled ? " text-decoration:line-through;" : string.Empty;

        var rows = new List<(string Caption, string Value)>();

        if (reason is not null) rows.Add((labels.Reason, Encode(reason)));

        rows.Add((labels.Barber, Encode(summary.StaffDisplayName)));

        if (!string.IsNullOrWhiteSpace(summary.CompanyPhone))
        {
            rows.Add((labels.Phone, BuildPhone(summary.CompanyPhone, labels.Call, appUrl)));
        }

        rows.Add((summary.Services.Count == 1 ? labels.Service : labels.Services, BuildServices(summary, culture, labels)));

        var html = string.Concat(rows.Select((row, index) => BuildRow(row.Caption, row.Value, index == 0)));
        html += BuildTotal(labels.Total, AppointmentEmailFormats.FormatMoney(summary.TotalPrice, summary.Currency, culture));

        if (summary.Notes is not null)
        {
            html += BuildRow(labels.Note, Encode(summary.Notes).ReplaceLineEndings("<br />"), first: false);
        }

        if (!string.IsNullOrWhiteSpace(summary.CompanyAddress))
        {
            html += BuildRow(labels.Address, Encode(summary.CompanyAddress), first: false);
        }

        // `separate`: the frame's `collapse` drops the radius.
        return $"""
                        <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%" style="border:1px solid {EmailShell.Border}; border-radius:12px; border-collapse:separate;">
                          <tr>
                            <td class="kc-card-pad" style="padding:20px 24px; border-bottom:1px solid {EmailShell.Border};">
                              <div style="font-family:{EmailShell.Serif}; font-size:22px; line-height:28px; font-weight:700; color:{EmailShell.TextPrimary};">{Encode(summary.CompanyName)}</div>
                              <div style="padding-top:8px; font-family:{EmailShell.Sans}; font-size:18px; line-height:26px; font-weight:700; color:{EmailShell.TextPrimary};{strike}">{Encode(date)}</div>
                              <div style="font-family:{EmailShell.Sans}; font-size:16px; line-height:24px; color:{EmailShell.TextSecondary};{strike}">{Encode($"{start} – {end}")}</div>
                            </td>
                          </tr>
                          <tr>
                            <td class="kc-card-pad" style="padding:4px 24px;">
                              <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%">
                {html}
                              </table>
                            </td>
                          </tr>
                        </table>
                """;
    }

    /// <param name="value">Already-safe HTML.</param>
    // Each row is a table of its own, so a phone stacks it without the total row's columns.
    private static string BuildRow(string caption, string value, bool first) =>
        WrapRow(first, $"""
                                        <td class="kc-cap" width="128" valign="top" style="padding:14px 16px 14px 0; font-family:{EmailShell.Sans}; font-size:16px; line-height:24px; color:{EmailShell.TextSecondary};">{Encode(caption)}</td>
                                        <td class="kc-val" valign="top" style="padding:14px 0; font-family:{EmailShell.Sans}; font-size:16px; line-height:24px; color:{EmailShell.TextPrimary};">{value}</td>
                        """);

    // Caption and amount stay on one line on a phone too, as on the page.
    private static string BuildTotal(string caption, string amount) =>
        WrapRow(first: false, $"""
                                        <td valign="middle" style="padding:14px 16px 14px 0; font-family:{EmailShell.Sans}; font-size:16px; line-height:24px; font-weight:700; color:{EmailShell.TextPrimary};">{Encode(caption)}</td>
                                        <td align="right" valign="middle" style="padding:14px 0; font-family:{EmailShell.Sans}; font-size:18px; line-height:26px; font-weight:700; color:{EmailShell.TextPrimary}; white-space:nowrap;">{Encode(amount)}</td>
                                   """);

    private static string WrapRow(bool first, string cells) =>
        $"""
                                <tr>
                                  <td style="{(first ? string.Empty : $"border-top:1px solid {EmailShell.BorderSubtle};")}">
                                    <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%">
                                      <tr>
         {cells}
                                      </tr>
                                    </table>
                                  </td>
                                </tr>
         """;

    private static string BuildServices(AppointmentSummary summary, CultureInfo culture, Labels labels)
    {
        var lines = string.Concat(summary.Services.Select((service, index) =>
        {
            var top = index == 0 ? 0 : 6;
            var price = AppointmentEmailFormats.FormatMoney(service.Price, summary.Currency, culture);

            return $"""
                      <tr>
                        <td valign="top" style="padding:{top}px 0 0; font-family:{EmailShell.Sans}; font-size:16px; line-height:24px; color:{EmailShell.TextPrimary};"><span style="font-weight:500;">{Encode(service.Name)}</span> <span style="font-size:14px; color:{EmailShell.TextMuted};">· {Encode(labels.FormatMinutes(service.DurationMinutes))}</span></td>
                        <td align="right" valign="top" style="padding:{top}px 0 0 12px; font-family:{EmailShell.Sans}; font-size:16px; line-height:24px; color:{EmailShell.TextPrimary}; white-space:nowrap;">{Encode(price)}</td>
                      </tr>
                    """;
        }));

        return $"""
                <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%">
                {lines}
                </table>
                """;
    }

    private static string BuildPhone(string phone, string call, string? appUrl) =>
        $"""
         <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%">
           <tr>
             <td valign="middle" style="font-family:{EmailShell.Sans}; font-size:16px; line-height:24px; color:{EmailShell.TextPrimary};">{Encode(phone)}</td>
             <td width="1" align="right" valign="middle" style="padding-left:12px;">
         {EmailShell.BuildButton(call, $"tel:{NotDialable().Replace(phone, string.Empty)}", EmailShell.GetIcon(appUrl, "phone.png", 16, 16), outlined: true, small: true)}
             </td>
           </tr>
         </table>
         """;

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    // Matches the web app's `telHref`: digits and a leading plus survive.
    [GeneratedRegex(@"[^\d+]")]
    private static partial Regex NotDialable();

    /// <summary>The account page's captions, in the customer's language.</summary>
    private sealed record Labels(
        string Reason,
        string Barber,
        string Phone,
        string Call,
        string Service,
        string Services,
        string Total,
        string Note,
        string Address,
        bool Greek)
    {
        public static Labels Create(bool greek) => greek
            ? new Labels("Λόγος", "Κουρέας", "Τηλέφωνο", "Κλήση", "Υπηρεσία", "Υπηρεσίες", "Σύνολο", "Η σημείωσή σου", "Διεύθυνση", greek)
            : new Labels("Reason", "Barber", "Phone", "Call", "Service", "Services", "Total", "Your note", "Address", greek);

        public string FormatMinutes(int minutes) =>
            $"{minutes.ToString(CultureInfo.InvariantCulture)} {(Greek ? "λεπτά" : "min")}";
    }
}
