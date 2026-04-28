using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace LetsLearn.UseCases.Services
{
    public class EmailService : IEmailService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IConfiguration configuration)
        {
            _host = configuration["Smtp:Host"] ?? "smtp.gmail.com";
            _port = int.Parse(configuration["Smtp:Port"] ?? "587");
            _username = configuration["Smtp:Username"] ?? "";
            _password = configuration["Smtp:Password"] ?? "";
            _fromEmail = configuration["Smtp:FromEmail"] ?? _username;
            _fromName = configuration["Smtp:FromName"] ?? "LetsLearn";
        }

        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            using var client = new SmtpClient(_host, _port)
            {
                Credentials = new NetworkCredential(_username, _password),
                EnableSsl = true
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
        }

        // ═══════════════════════════════════════════════════════════════
        // SHARED: Logo block (reused across all templates)
        // ═══════════════════════════════════════════════════════════════
        private static string LogoBlock(string textColor = "#FFFFFF") => $@"
<table cellpadding='0' cellspacing='0' border='0' style='margin:0 auto 0;'>
  <tr>
    <td style='text-align:center;vertical-align:middle;'>
      <span style='font-size:15px;font-weight:900;color:{textColor};letter-spacing:3px;text-transform:uppercase;font-family:Georgia,serif;'>LETS<span style='opacity:0.55;'>LEARN</span></span>
    </td>
  </tr>
</table>";

        // ═══════════════════════════════════════════════════════════════
        // PASSWORD RESET
        // ═══════════════════════════════════════════════════════════════
        public async Task SendPasswordResetAsync(string toEmail, string username, string newPassword)
        {
            var subject = "Your LetsLearn password has been reset";

            var body = $@"<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width,initial-scale=1.0'>
<title>Password Reset — LetsLearn</title>
</head>
<body style='margin:0;padding:0;background-color:#0A0A0F;font-family:Georgia,serif;'>

<table width='100%' cellpadding='0' cellspacing='0' border='0'>
<tr><td align='center' style='padding:48px 16px 56px;background-color:#0A0A0F;'>

  <!-- Card -->
  <table cellpadding='0' cellspacing='0' border='0' style='max-width:520px;width:100%;'>

    <!-- Top logo bar -->
    <tr>
      <td style='padding-bottom:32px;text-align:center;'>
        {LogoBlock("#FFFFFF")}
      </td>
    </tr>

    <!-- Main card -->
    <tr>
      <td style='background:linear-gradient(160deg,#1A1A2E 0%,#16213E 50%,#0F3460 100%);border-radius:24px;overflow:hidden;border:1px solid rgba(255,255,255,0.07);'>

        <!-- Decorative top stripe -->
        <table cellpadding='0' cellspacing='0' border='0' style='width:100%;'>
          <tr>
            <td style='background:linear-gradient(90deg,#667EEA,#764BA2,#F093FB);height:3px;font-size:0;line-height:0;'>&nbsp;</td>
          </tr>
        </table>

        <!-- Body -->
        <table cellpadding='0' cellspacing='0' border='0' style='width:100%;'>
          <tr>
            <td style='padding:52px 48px 48px;'>

              <!-- Lock icon circle -->
              <table cellpadding='0' cellspacing='0' border='0' style='margin:0 auto 32px;'>
                <tr>
                  <td style='width:80px;height:80px;border-radius:50%;background:linear-gradient(135deg,#667EEA22,#764BA222);border:1px solid rgba(102,126,234,0.3);text-align:center;vertical-align:middle;'>
                    <span style='font-size:34px;line-height:80px;'>🔐</span>
                  </td>
                </tr>
              </table>

              <!-- Headline -->
              <h1 style='font-size:32px;font-weight:400;color:#F8F9FF;text-align:center;margin:0 0 8px;letter-spacing:-0.5px;font-style:italic;'>
                Password Reset
              </h1>
              <p style='font-size:14px;color:rgba(255,255,255,0.4);text-align:center;margin:0 0 40px;letter-spacing:1px;text-transform:uppercase;font-family:Arial,sans-serif;font-style:normal;'>
                Account Security Notice
              </p>

              <!-- Hi user -->
              <p style='font-size:16px;color:rgba(255,255,255,0.75);text-align:center;margin:0 0 36px;line-height:1.7;font-family:Arial,sans-serif;'>
                Hi <strong style='color:#A78BFA;font-weight:600;'>{username}</strong>,<br>
                your temporary password is ready below.
              </p>

              <!-- Password display box -->
              <table cellpadding='0' cellspacing='0' border='0' style='width:100%;margin-bottom:16px;'>
                <tr>
                  <td style='background-color:#0D0D1A;border:1px solid rgba(102,126,234,0.25);border-radius:16px;padding:32px 24px;text-align:center;'>
                    <p style='font-size:11px;font-weight:700;letter-spacing:3px;text-transform:uppercase;color:rgba(255,255,255,0.3);margin:0 0 16px;font-family:Arial,sans-serif;'>
                      Temporary Password
                    </p>
                    <div style='display:inline-block;background:linear-gradient(135deg,#667EEA,#764BA2);border-radius:12px;padding:1px;'>
                      <div style='background-color:#0D0D1A;border-radius:11px;padding:14px 32px;'>
                        <span style='font-size:26px;font-weight:700;color:#E2D9F3;letter-spacing:5px;font-family:Consolas,Courier New,monospace;'>{newPassword}</span>
                      </div>
                    </div>
                  </td>
                </tr>
              </table>

              <!-- Warning pill -->
              <table cellpadding='0' cellspacing='0' border='0' style='margin:0 auto 40px;'>
                <tr>
                  <td style='background-color:rgba(251,191,36,0.1);border:1px solid rgba(251,191,36,0.25);border-radius:100px;padding:10px 22px;text-align:center;'>
                    <span style='font-size:12px;color:#FCD34D;font-family:Arial,sans-serif;letter-spacing:0.3px;'>
                      ⚠ &nbsp;Login and change this password immediately
                    </span>
                  </td>
                </tr>
              </table>

              <!-- CTA Button -->
              <table cellpadding='0' cellspacing='0' border='0' style='margin:0 auto;'>
                <tr>
                  <td style='background:linear-gradient(135deg,#667EEA,#764BA2);border-radius:12px;padding:1px;'>
                    <a href='#' style='display:block;background:linear-gradient(135deg,#667EEA,#764BA2);color:#ffffff;font-weight:700;font-size:14px;padding:16px 44px;border-radius:11px;text-decoration:none;letter-spacing:0.5px;font-family:Arial,sans-serif;text-align:center;'>
                      Sign In to LetsLearn →
                    </a>
                  </td>
                </tr>
              </table>

            </td>
          </tr>
        </table>

      </td>
    </tr>

    <!-- Footer -->
    <tr>
      <td style='padding:28px 0 0;text-align:center;'>
        <p style='font-size:11px;color:rgba(255,255,255,0.2);margin:0;letter-spacing:0.5px;font-family:Arial,sans-serif;line-height:1.8;'>
          If you didn't request this, please ignore this email.<br>
          Your account remains secure until the password is used.<br><br>
          © 2024 LetsLearn · All rights reserved
        </p>
      </td>
    </tr>

  </table>
</td></tr>
</table>

</body>
</html>";

            await SendAsync(toEmail, subject, body);
        }

        // ═══════════════════════════════════════════════════════════════
        // NEW TOPIC NOTIFICATION
        // ═══════════════════════════════════════════════════════════════
        public async Task SendNewTopicNotificationAsync(
            string toEmail,
            string studentName,
            string courseTitle,
            string topicTitle,
            string topicType,
            DateTime? openDate,
            DateTime? closeDate)
        {
            var (icon, label, gradient, accentLight, tag) = topicType.ToLower() switch
            {
                "assignment" => ("✏️", "Assignment", "linear-gradient(135deg,#7C3AED,#A855F7)", "#EDE9FE", "ASSIGNMENT"),
                "quiz"       => ("⚡", "Quiz",       "linear-gradient(135deg,#BE185D,#EC4899)", "#FCE7F3", "QUIZ"),
                "meeting"    => ("🎬", "Meeting",    "linear-gradient(135deg,#1D4ED8,#60A5FA)", "#DBEAFE", "LIVE"),
                _            => ("📂", topicType,   "linear-gradient(135deg,#065F46,#34D399)", "#D1FAE5", "NEW")
            };

            string dateSection = "";
            if (openDate.HasValue || closeDate.HasValue)
            {
                string openPart = openDate.HasValue ? $@"
      <table cellpadding='0' cellspacing='0' border='0' style='width:{(closeDate.HasValue ? "50" : "100")}%;'>
        <tr>
          <td style='padding:18px 20px;border-right:{(closeDate.HasValue ? "1px solid rgba(255,255,255,0.06)" : "none")};'>
            <p style='font-size:10px;letter-spacing:2px;text-transform:uppercase;color:rgba(255,255,255,0.35);margin:0 0 6px;font-family:Arial,sans-serif;'>Opens</p>
            <p style='font-size:16px;font-weight:700;color:#E2E8F0;margin:0;font-family:Arial,sans-serif;'>{openDate!.Value:MMM d}</p>
            <p style='font-size:12px;color:rgba(255,255,255,0.45);margin:2px 0 0;font-family:Arial,sans-serif;'>{openDate!.Value:h:mm tt}</p>
          </td>
        </tr>
      </table>" : "";

                string closePart = closeDate.HasValue ? $@"
      <table cellpadding='0' cellspacing='0' border='0' style='width:{(openDate.HasValue ? "50" : "100")}%;'>
        <tr>
          <td style='padding:18px 20px;'>
            <p style='font-size:10px;letter-spacing:2px;text-transform:uppercase;color:rgba(252,165,165,0.7);margin:0 0 6px;font-family:Arial,sans-serif;'>Deadline</p>
            <p style='font-size:16px;font-weight:700;color:#FCA5A5;margin:0;font-family:Arial,sans-serif;'>{closeDate!.Value:MMM d}</p>
            <p style='font-size:12px;color:rgba(252,165,165,0.6);margin:2px 0 0;font-family:Arial,sans-serif;'>{closeDate!.Value:h:mm tt}</p>
          </td>
        </tr>
      </table>" : "";

                dateSection = $@"
  <!-- Dates row -->
  <table cellpadding='0' cellspacing='0' border='0' style='width:100%;border-radius:12px;overflow:hidden;background-color:#12121F;border:1px solid rgba(255,255,255,0.06);margin-bottom:32px;'>
    <tr>
      {openPart}
      {closePart}
    </tr>
  </table>";
            }

            var subject = $"[LetsLearn] New {label}: {topicTitle}";

            var body = $@"<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width,initial-scale=1.0'>
<title>New Activity — LetsLearn</title>
</head>
<body style='margin:0;padding:0;background-color:#07070F;font-family:Georgia,serif;'>

<table width='100%' cellpadding='0' cellspacing='0' border='0'>
<tr><td align='center' style='padding:48px 16px 56px;background-color:#07070F;'>
  <table cellpadding='0' cellspacing='0' border='0' style='max-width:520px;width:100%;'>

    <!-- Logo -->
    <tr>
      <td style='padding-bottom:28px;text-align:center;'>
        {LogoBlock("#FFFFFF")}
      </td>
    </tr>

    <!-- Card -->
    <tr>
      <td style='border-radius:24px;overflow:hidden;background-color:#0E0E1C;border:1px solid rgba(255,255,255,0.06);'>

        <!-- Gradient Header -->
        <table cellpadding='0' cellspacing='0' border='0' style='width:100%;'>
          <tr>
            <td style='background:{gradient};padding:44px 48px 40px;text-align:center;'>

              <!-- Type badge -->
              <table cellpadding='0' cellspacing='0' border='0' style='margin:0 auto 18px;'>
                <tr>
                  <td style='background-color:rgba(255,255,255,0.18);border-radius:100px;padding:6px 18px;'>
                    <span style='font-size:10px;font-weight:700;color:#ffffff;letter-spacing:3px;font-family:Arial,sans-serif;'>{tag}</span>
                  </td>
                </tr>
              </table>

              <span style='font-size:52px;display:block;margin-bottom:14px;line-height:1;'>{icon}</span>

              <h1 style='font-size:13px;font-weight:700;color:rgba(255,255,255,0.65);margin:0;letter-spacing:2px;text-transform:uppercase;font-family:Arial,sans-serif;'>
                New activity posted
              </h1>
            </td>
          </tr>
        </table>

        <!-- Body -->
        <table cellpadding='0' cellspacing='0' border='0' style='width:100%;'>
          <tr>
            <td style='padding:40px 48px 44px;'>

              <!-- Greeting -->
              <p style='font-size:16px;color:rgba(255,255,255,0.65);margin:0 0 28px;line-height:1.7;font-family:Arial,sans-serif;'>
                Hey <strong style='color:#F1F5FF;font-weight:600;'>{studentName}</strong> — your teacher just posted something new. Don't miss it.
              </p>

              <!-- Topic Card -->
              <table cellpadding='0' cellspacing='0' border='0' style='width:100%;margin-bottom:28px;'>
                <tr>
                  <td style='background-color:#12121F;border-radius:16px;padding:28px 28px 24px;border:1px solid rgba(255,255,255,0.06);'>
                    <p style='font-size:10px;letter-spacing:2.5px;text-transform:uppercase;color:rgba(255,255,255,0.3);margin:0 0 10px;font-family:Arial,sans-serif;'>
                      📚 &nbsp;{courseTitle}
                    </p>
                    <h2 style='font-size:24px;font-weight:700;color:#F1F5FF;margin:0 0 18px;line-height:1.3;font-style:normal;text-transform:uppercase;letter-spacing:1px;'>
                        {topicTitle}
                      </h2>
                    <table cellpadding='0' cellspacing='0' border='0'>
                      <tr>
                        <td style='background:{gradient};border-radius:6px;padding:1px;'>
                          <span style='display:block;background-color:#12121F;border-radius:5px;padding:4px 14px;font-size:10px;font-weight:700;color:rgba(255,255,255,0.6);letter-spacing:2px;font-family:Arial,sans-serif;'>
                            {label.ToUpper()}
                          </span>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>

              <!-- Dates -->
              {dateSection}

              <!-- CTA -->
             <p style='font-size:13px;color:rgba(255,255,255,0.4);text-align:center;margin:0;font-family:Arial,sans-serif;line-height:1.7;border-top:1px solid rgba(255,255,255,0.06);padding-top:24px;'>
  📌 &nbsp;Remember to submit your assignment <strong style='color:rgba(255,255,255,0.7);'>{topicTitle}</strong> before the deadline, don't let it slip!
</p>

            </td>
          </tr>
        </table>

      </td>
    </tr>

    <!-- Footer -->
    <tr>
      <td style='padding:28px 0 0;text-align:center;'>
        <p style='font-size:11px;color:rgba(255,255,255,0.18);margin:0;letter-spacing:0.5px;font-family:Arial,sans-serif;line-height:1.8;'>
          You received this because you're enrolled in {courseTitle}<br>
          © 2024 LetsLearn · <a href='#' style='color:rgba(255,255,255,0.3);text-decoration:none;'>Manage Notifications</a>
        </p>
      </td>
    </tr>

  </table>
</td></tr>
</table>

</body>
</html>";

            await SendAsync(toEmail, subject, body);
        }

        // ═══════════════════════════════════════════════════════════════
        // DEADLINE REMINDER
        // ═══════════════════════════════════════════════════════════════
        public async Task SendDeadlineReminderAsync(
            string toEmail,
            string studentName,
            string courseTitle,
            string topicTitle,
            string topicType,
            DateTime deadline,
            string? meetingLink)
        {
            var now = DateTime.UtcNow;
            var totalHours = (deadline - now).TotalHours;
            var isOverdue = totalHours < 0;
            var isUrgent  = totalHours > 0 && totalHours <= 1;
            var isSoon    = totalHours > 1 && totalHours <= 24;

            var (icon, label, ctaLabel) = topicType.ToLower() switch
            {
                "assignment" => ("✏️", "Assignment", "Submit Now"),
                "quiz"       => ("⚡", "Quiz",       "Take Quiz"),
                "meeting"    => ("🎬", "Meeting",    "Join Meeting"),
                _            => ("📂", topicType,   "View Now")
            };

            // Color scheme per urgency
            string gradient, accentHex, urgencyBg, urgencyLabel, urgencyNote;
            if (isOverdue)
            {
                gradient     = "linear-gradient(135deg,#7F1D1D,#DC2626)";
                accentHex    = "#EF4444";
                urgencyBg    = "#1A0505";
                urgencyLabel = "DEADLINE PASSED";
                urgencyNote  = "Submit as soon as possible — contact your teacher if needed.";
            }
            else if (isUrgent)
            {
                gradient     = "linear-gradient(135deg,#7C2D12,#EA580C)";
                accentHex    = "#F97316";
                urgencyBg    = "#1A0B05";
                urgencyLabel = $"{Math.Ceiling(totalHours * 60):0} MINUTES LEFT";
                urgencyNote  = "Stop everything. Submit right now.";
            }
            else if (isSoon)
            {
                gradient     = "linear-gradient(135deg,#713F12,#CA8A04)";
                accentHex    = "#FACC15";
                urgencyBg    = "#16110A";
                urgencyLabel = $"{Math.Ceiling(totalHours):0} HOURS LEFT";
                urgencyNote  = "Don't leave it to the last minute.";
            }
            else
            {
                gradient     = "linear-gradient(135deg,#1E3A5F,#3B82F6)";
                accentHex    = "#60A5FA";
                urgencyBg    = "#07111F";
                urgencyLabel = $"{Math.Ceiling(totalHours / 24):0} DAYS LEFT";
                urgencyNote  = $"Due: {deadline:MMMM d, yyyy · h:mm tt} UTC";
            }

            // Progress bar (time remaining 0–100%)
            var totalWindow = 48.0;
            var elapsed = Math.Max(0, totalWindow - Math.Max(0, totalHours));
            var pctConsumed = Math.Min(100, elapsed / totalWindow * 100);
            var remaining = Math.Max(0, 100 - pctConsumed);
            var barFill = Math.Max(3, (int)remaining);

            // Meeting join button
            string meetingSection = "";
            if (topicType.ToLower() == "meeting" && !string.IsNullOrEmpty(meetingLink))
            {
                meetingSection = $@"
  <table cellpadding='0' cellspacing='0' border='0' style='width:100%;margin-bottom:16px;'>
    <tr>
      <td style='text-align:center;'>
        <a href='{meetingLink}' target='_blank'
           style='display:inline-block;background:linear-gradient(135deg,#065F46,#10B981);color:#ffffff;font-weight:700;font-size:14px;padding:15px 44px;border-radius:12px;text-decoration:none;letter-spacing:0.5px;font-family:Arial,sans-serif;'>
          🎬 &nbsp;Join the Meeting Now →
        </a>
      </td>
    </tr>
  </table>";
            }

            var subject = isOverdue
                ? $"[LetsLearn] ⚠ OVERDUE: {topicTitle}"
                : $"[LetsLearn] ⏰ {urgencyLabel} — {topicTitle}";

            var body = $@"<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width,initial-scale=1.0'>
<title>Deadline Reminder — LetsLearn</title>
</head>
<body style='margin:0;padding:0;background-color:#07070F;font-family:Georgia,serif;'>

<table width='100%' cellpadding='0' cellspacing='0' border='0'>
<tr><td align='center' style='padding:48px 16px 56px;background-color:#07070F;'>
  <table cellpadding='0' cellspacing='0' border='0' style='max-width:520px;width:100%;'>

    <!-- Logo -->
    <tr>
      <td style='padding-bottom:28px;text-align:center;'>
        {LogoBlock("#FFFFFF")}
      </td>
    </tr>

    <!-- Card -->
    <tr>
      <td style='border-radius:24px;overflow:hidden;background-color:#0E0E1C;border:1px solid rgba(255,255,255,0.06);'>

        <!-- Header -->
        <table cellpadding='0' cellspacing='0' border='0' style='width:100%;'>
          <tr>
            <td style='background:{gradient};padding:40px 48px 36px;text-align:center;'>

              <span style='font-size:50px;display:block;margin-bottom:14px;line-height:1;'>{icon}</span>

              <!-- Urgency pill -->
              <table cellpadding='0' cellspacing='0' border='0' style='margin:0 auto 12px;'>
                <tr>
                  <td style='background-color:rgba(0,0,0,0.25);border-radius:100px;padding:8px 22px;border:1px solid rgba(255,255,255,0.15);'>
                    <span style='font-size:13px;font-weight:900;color:#ffffff;letter-spacing:2.5px;font-family:Arial,sans-serif;'>
                      {urgencyLabel}
                    </span>
                  </td>
                </tr>
              </table>

              <p style='font-size:13px;color:rgba(255,255,255,0.6);margin:0;font-family:Arial,sans-serif;letter-spacing:0.5px;'>
                {(isOverdue ? "This deadline has already passed" : "Time is ticking — take action now")}
              </p>
            </td>
          </tr>
        </table>

        <!-- Body -->
        <table cellpadding='0' cellspacing='0' border='0' style='width:100%;'>
          <tr>
            <td style='padding:40px 48px 44px;'>

              <!-- Greeting -->
              <p style='font-size:16px;color:rgba(255,255,255,0.65);margin:0 0 28px;line-height:1.7;font-family:Arial,sans-serif;'>
                Hi <strong style='color:#F1F5FF;'>{studentName}</strong> — you have a pending <strong style='color:{accentHex};'>{label.ToLower()}</strong> in <strong style='color:#F1F5FF;'>{courseTitle}</strong>.
              </p>

              <!-- Topic Card with progress bar -->
              <table cellpadding='0' cellspacing='0' border='0' style='width:100%;margin-bottom:24px;'>
                <tr>
                  <td style='background-color:#12121F;border-radius:16px;padding:26px 28px;border:1px solid rgba(255,255,255,0.06);'>

                    <!-- Course label + type badge -->
                    <table cellpadding='0' cellspacing='0' border='0' style='width:100%;margin-bottom:12px;'>
                      <tr>
                        <td>
                          <span style='font-size:10px;letter-spacing:2px;text-transform:uppercase;color:rgba(255,255,255,0.3);font-family:Arial,sans-serif;'>
                            📚 &nbsp;{courseTitle}
                          </span>
                        </td>
                        <td style='text-align:right;'>
                          <span style='font-size:10px;font-weight:700;color:{accentHex};letter-spacing:1.5px;font-family:Arial,sans-serif;'>{label.ToUpper()}</span>
                        </td>
                      </tr>
                    </table>

                    <h2 style='font-size:22px;font-weight:400;color:#F1F5FF;margin:0 0 22px;font-style:italic;line-height:1.3;'>
                      {topicTitle}
                    </h2>

                    <!-- Progress bar -->
                    <table cellpadding='0' cellspacing='0' border='0' style='width:100%;margin-bottom:6px;'>
                      <tr>
                        <td style='background-color:rgba(255,255,255,0.06);border-radius:100px;height:6px;overflow:hidden;font-size:0;line-height:0;'>
                          <div style='background:{gradient};height:6px;border-radius:100px;width:{barFill}%;'></div>
                        </td>
                      </tr>
                    </table>
                    <table cellpadding='0' cellspacing='0' border='0' style='width:100%;'>
                      <tr>
                        <td><span style='font-size:10px;color:rgba(255,255,255,0.25);font-family:Arial,sans-serif;'>Time elapsed</span></td>
                        <td style='text-align:right;'><span style='font-size:10px;font-weight:700;color:{accentHex};font-family:Arial,sans-serif;'>{remaining:0}% remaining</span></td>
                      </tr>
                    </table>

                  </td>
                </tr>
              </table>

              <!-- Urgency note box -->
              <table cellpadding='0' cellspacing='0' border='0' style='width:100%;margin-bottom:32px;'>
                <tr>
                  <td style='background-color:{urgencyBg};border-left:3px solid {accentHex};border-radius:0 12px 12px 0;padding:16px 20px;'>
                    <p style='font-size:13px;color:{accentHex};font-weight:700;margin:0 0 2px;letter-spacing:0.5px;font-family:Arial,sans-serif;'>
                      ⚠ Deadline: {deadline:MMMM d, yyyy · h:mm tt} UTC
                    </p>
                    <p style='font-size:12px;color:rgba(255,255,255,0.45);margin:0;font-family:Arial,sans-serif;'>
                      {urgencyNote}
                    </p>
                  </td>
                </tr>
              </table>

              <!-- Meeting join (optional) -->
              {meetingSection}

              <!-- Main CTA -->
              <table cellpadding='0' cellspacing='0' border='0' style='width:100%;'>
                <tr>
                  <td style='text-align:center;'>
                    <table cellpadding='0' cellspacing='0' border='0' style='margin:0 auto;'>
                      <tr>
                        <td style='background:{gradient};border-radius:12px;padding:1px;'>
                          <a href='#' style='display:block;background:{gradient};color:#ffffff;font-weight:700;font-size:14px;padding:16px 48px;border-radius:11px;text-decoration:none;letter-spacing:0.5px;font-family:Arial,sans-serif;text-align:center;'>
                            {ctaLabel} →
                          </a>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>

            </td>
          </tr>
        </table>

      </td>
    </tr>

    <!-- Footer -->
    <tr>
      <td style='padding:28px 0 0;text-align:center;'>
        <p style='font-size:11px;color:rgba(255,255,255,0.18);margin:0;letter-spacing:0.5px;font-family:Arial,sans-serif;line-height:1.8;'>
          🤖 &nbsp;Automated reminder from LetsLearn<br>
          © 2024 LetsLearn · <a href='#' style='color:rgba(255,255,255,0.3);text-decoration:none;'>Unsubscribe</a>
        </p>
      </td>
    </tr>

  </table>
</td></tr>
</table>

</body>
</html>";

            await SendAsync(toEmail, subject, body);
        }
    }
}