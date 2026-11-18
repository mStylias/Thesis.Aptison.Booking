using System.Net;
using Aptison.Booking.Application.Abstractions.Messaging;

namespace Aptison.Booking.Application.Messaging;

/// <summary>
/// The frame around every HTML mail this API sends. Nested tables and inline styles because Outlook
/// renders with Word's engine and Gmail strips <c>&lt;head&gt;</c> styles; the one
/// <c>&lt;style&gt;</c> block is progressive enhancement.
/// <para>
/// The colours below are a third copy — the Angular workspace's <c>_tokens.scss</c>/<c>_light.scss</c>
/// and the Keycloak theme's <c>template.ftl</c> hold the others. Change them together.
/// </para>
/// </summary>
public static class EmailShell
{
    public const string Accent = "#9e5f28";
    public const string Ground = "#fbf8f2";
    public const string Surface = "#ffffff";
    public const string TextPrimary = "#1c1814";
    public const string TextSecondary = "#5a4f43";
    public const string TextMuted = "#7a6e5f";
    public const string AccentSubtle = "#fbf1e8";
    public const string Border = "#e6dece";
    public const string BorderSubtle = "#ece5d8";
    public const string BorderStrong = "#d8cdb8";

    public const string Serif = "Alegreya,Georgia,'Times New Roman',serif";
    public const string Sans = "'Alegreya Sans',-apple-system,'Segoe UI',Helvetica,Arial,sans-serif";

    /// <summary>
    /// Wraps <paramref name="content"/> — already-safe HTML — in the frame. Every other string
    /// parameter is plain text and is encoded here.
    /// </summary>
    public static string Wrap(
        string languageTag,
        string preheader,
        string content,
        string footer,
        EmailBrandingOptions branding,
        EmailLink? footerLink = null)
    {
        var lang = WebUtility.HtmlEncode(languageTag);
        var brandName = branding.BrandName?.Trim() ?? string.Empty;
        var appUrl = NormalizeAppUrl(branding.AppUrl);

        // `$$` so a single brace is literal CSS and `{{…}}` is an interpolation.
        return $$"""
                <!DOCTYPE html>
                <html lang="{{lang}}" xmlns="http://www.w3.org/1999/xhtml">
                <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <meta http-equiv="X-UA-Compatible" content="IE=edge" />
                <!-- Tells a dark-mode client to tone this design rather than invert it. -->
                <meta name="color-scheme" content="light" />
                <meta name="supported-color-schemes" content="light" />
                <title>{{WebUtility.HtmlEncode(preheader)}}</title>
                <style>
                  /* Every rule here restates an inline style. */
                  body { margin: 0 !important; padding: 0 !important; width: 100% !important; }
                  table { border-collapse: collapse; mso-table-lspace: 0pt; mso-table-rspace: 0pt; }
                  img { border: 0; line-height: 100%; outline: none; text-decoration: none; }
                  a { color: {{Accent}}; }
                  /* Apple Mail and iOS turn dates, addresses and numbers into blue links of their own. */
                  a[x-apple-data-detectors] { color: inherit !important; text-decoration: none !important; }
                  @media only screen and (max-width: 620px) {
                    .kc-shell { width: 100% !important; }
                    .kc-pad { padding-left: 24px !important; padding-right: 24px !important; }
                    .kc-code { font-size: 32px !important; letter-spacing: 6px !important; }
                    .kc-card-pad { padding-left: 16px !important; padding-right: 16px !important; }
                    .kc-cap { display: block !important; width: 100% !important; padding: 14px 0 2px !important; }
                    .kc-val { display: block !important; width: 100% !important; padding: 0 0 14px !important; }
                    .kc-stack { display: block !important; width: 100% !important; padding: 0 !important; }
                    .kc-stack-gap { padding-top: 12px !important; }
                  }
                </style>
                </head>
                <body style="margin:0; padding:0; width:100%; background-color:{{Ground}}; -webkit-text-size-adjust:100%; -ms-text-size-adjust:100%;">

                {{BuildPreheader(preheader)}}

                <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%" style="background-color:{{Ground}};">
                  <tr>
                    <td align="center" style="padding:32px 12px;">

                      <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="600" class="kc-shell" style="width:600px; max-width:600px;">
                {{BuildWordmark(brandName, appUrl)}}
                        <tr>
                          <td class="kc-pad" style="background-color:{{Surface}}; border-radius:14px; padding:40px;">
                {{content}}
                          </td>
                        </tr>

                        <tr>
                          <td class="kc-pad" align="center" style="padding:24px 40px 0; font-family:{{Sans}}; font-size:13px; line-height:20px; color:{{TextMuted}};">
                            {{WebUtility.HtmlEncode(footer)}}{{BuildFooterLink(footerLink)}}
                          </td>
                        </tr>

                      </table>

                    </td>
                  </tr>
                </table>

                </body>
                </html>
                """;
    }

    private static string BuildFooterLink(EmailLink? link) => link is null
        ? string.Empty
        : $"""<br /><a href="{WebUtility.HtmlEncode(link.Url)}" target="_blank" rel="noopener" style="color:{TextMuted}; text-decoration:underline;">{WebUtility.HtmlEncode(link.Label)}</a>""";

    // Padded with zero-width joiners so the client does not pull body copy in after it.
    private static string BuildPreheader(string preheader)
    {
        if (string.IsNullOrWhiteSpace(preheader)) return string.Empty;

        var padding = string.Concat(Enumerable.Repeat("&#847;&zwnj;&nbsp;", 10));

        return $"""
                <div style="display:none; font-size:1px; color:{Ground}; line-height:1px; max-height:0; max-width:0; opacity:0; overflow:hidden; mso-hide:all;">
                  {WebUtility.HtmlEncode(preheader)}{padding}
                </div>
                """;
    }

    // Type, not an image: most clients block remote images, so the brand must survive without one.
    private static string BuildWordmark(string brandName, string? appUrl)
    {
        if (brandName.Length == 0) return string.Empty;

        var safeName = WebUtility.HtmlEncode(brandName);

        // Nested table: cell `vertical-align` is the only centre-line alignment mail clients honour.
        var icon = appUrl is null
            ? string.Empty
            : $"""
                              <td valign="middle" style="padding-right:10px; line-height:0;">
                                 <a href="{appUrl}" target="_blank" rel="noopener">
                                   <!-- Empty alt: the cell beside it already spells the brand name. -->
                                   <img src="{appUrl}/brand/koureia_icon.png" width="32" height="32" alt="" style="display:block; border:0; outline:none; text-decoration:none; width:32px; height:32px;" />
                                 </a>
                               </td>
               """;

        var word = appUrl is null
            ? $"""{safeName}<span style="color:{Accent};">.</span>"""
            : $"""<a href="{appUrl}" target="_blank" rel="noopener" style="color:{TextPrimary}; text-decoration:none;">{safeName}<span style="color:{Accent};">.</span></a>""";

        return $"""
                        <tr>
                          <td align="center" style="padding:0 0 24px;">
                            <table role="presentation" border="0" cellpadding="0" cellspacing="0" align="center">
                              <tr>
                {icon}
                                <td valign="middle">
                                  <span style="font-family:{Serif}; font-size:28px; line-height:32px; font-weight:700; letter-spacing:-0.01em; color:{TextPrimary};">{word}</span>
                                </td>
                              </tr>
                            </table>
                          </td>
                        </tr>

                """;
    }

    /// <summary>
    /// The site's button: primary, or outlined secondary. <paramref name="url"/> is absolute and not
    /// yet encoded. A <paramref name="block"/> button fills its cell.
    /// </summary>
    public static string BuildButton(
        string label,
        string url,
        EmailIcon? icon = null,
        bool outlined = false,
        bool block = false,
        bool small = false)
    {
        var cell = outlined
            ? $"border:1px solid {BorderStrong}; background-color:{Surface};"
            : $"background-color:{Accent};";
        var color = outlined ? TextPrimary : "#ffffff";
        var radius = small ? 8 : 10;
        // The outline's 1px comes out of the padding, so both variants keep one height. The table is
        // `separate` because the frame's `collapse` drops the radius of a bordered cell.
        var inset = outlined ? 1 : 0;
        var padding = small ? $"{5 - inset}px {12 - inset}px" : $"{12 - inset}px {24 - inset}px";
        var font = small ? "font-size:14px; line-height:20px;" : "font-size:17px; line-height:24px;";

        return $"""
                        <table role="presentation" border="0" cellpadding="0" cellspacing="0" align="center"{(block ? " width=\"100%\"" : string.Empty)} style="border-collapse:separate;">
                          <tr>
                            <td align="center" style="{cell} border-radius:{radius}px;">
                              <a href="{WebUtility.HtmlEncode(url)}" target="_blank" rel="noopener" style="display:{(block ? "block" : "inline-block")}; padding:{padding}; font-family:{Sans}; {font} font-weight:700; color:{color}; text-decoration:none; border-radius:{radius}px;">{BuildIcon(icon)}<span style="vertical-align:middle;">{WebUtility.HtmlEncode(label)}</span></a>
                            </td>
                          </tr>
                        </table>
                """;
    }

    /// <summary>Two buttons side by side, <paramref name="primary"/> on the right. A phone stacks them, primary on top.</summary>
    public static string BuildButtonPair(string primary, string secondary) =>
        // The primary comes first in the source, so it stacks on top. `dir="rtl"` puts it on the right
        // while the cells sit side by side.
        $"""
                        <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%" dir="rtl">
                          <tr>
                            <td class="kc-stack" dir="ltr" width="50%" valign="top" style="padding:0 0 0 6px;">
         {primary}
                            </td>
                            <td class="kc-stack kc-stack-gap" dir="ltr" width="50%" valign="top" style="padding:0 6px 0 0;">
         {secondary}
                            </td>
                          </tr>
                        </table>
         """;

    /// <summary>A PNG under the web app's <c>email/</c> folder. Null when the web app's address is not configured.</summary>
    public static EmailIcon? GetIcon(string? appUrl, string fileName, int width, int height) =>
        GetAppOrigin(appUrl) is { } origin ? new EmailIcon($"{origin}/email/{fileName}", width, height) : null;

    // Decoration only: a client that blocks images still shows the label.
    private static string BuildIcon(EmailIcon? icon) => icon is null
        ? string.Empty
        : $"""<img src="{WebUtility.HtmlEncode(icon.Url)}" width="{icon.Width}" height="{icon.Height}" alt="" style="display:inline-block; vertical-align:middle; border:0; outline:none; margin:0 8px 0 0; width:{icon.Width}px; height:{icon.Height}px;" />""";

    /// <summary>Absolute HTTP(S) only, since a relative link in an email resolves against the mail client. Not encoded.</summary>
    public static string? GetAppOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static string? NormalizeAppUrl(string? value) =>
        GetAppOrigin(value) is { } origin ? WebUtility.HtmlEncode(origin) : null;
}
