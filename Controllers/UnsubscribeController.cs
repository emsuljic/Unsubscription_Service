using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using MimeKit;
using System.Text.RegularExpressions;
using UnsubscribeService.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using UnsubscribeService.Models;
using UnsubscribeService.EmailTemplate;

namespace UnsubscribeService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UnsubscribeController : ControllerBase
    {
        #region << Fields >>

        private readonly ICustomMemoryCache _customMemoryCache;
        private readonly ITemplateService _templateService;
        private readonly List<string> _mailingList;
        private readonly string _unsubscribeCacheKeyPrefix;

        private const string EmailPattern = @"^[a-zA-Z]+\.[a-zA-Z]+@gmail\.com$";

        #endregion

        #region << Constructor >>
        public UnsubscribeController(
            ICustomMemoryCache customMemoryCache, 
            IConfiguration configuration,
            ITemplateService templateService)
        {
            _customMemoryCache = customMemoryCache;
            _mailingList = configuration.GetSection("MailingList").Get<List<string>>();
            _unsubscribeCacheKeyPrefix = configuration.GetValue<string>("UnsubscribeCacheKeyPrefix");
            _templateService = templateService;

            if (_mailingList == null)
                throw new ExceptionHandler("Mailing list not configured.");
        }
        #endregion


        #region << Public methods >>


        /// <summary>
        /// User clicks the unsubscribe link.
        /// This method processes the unsubscribe request by validating the provided email
        /// and timestamp, generating a token, and redirecting the user to the confirmation page.
        /// </summary>
        /// <param name="id">The email address of the user attempting to unsubscribe (formatted as firstname.lastname@gmail.com).</param>
        /// <param name="htmlTemplate">The UUID of the HTML template for the unsubscribe page.</param>
        /// <param name="t">The timestamp indicating when the unsubscribe link was generated.</param>
        /// <returns>An <see cref="IActionResult"/> that redirects the user to the confirmation page.</returns>
        ///  GET https://myservice-x.net/unsubscribe?id={{email}}&htmlTemplate={{uuid}}?t={{timestamp}}
        [HttpGet]
        [EnableRateLimiting("BasicRateLimiter")]
        public IActionResult Unsubscribe(string id, string htmlTemplate, string t)
        {
            var email = id;

            ValidateUnsubscribeInputs(email, t);

            if (!_mailingList.Contains(email))
                throw new ExceptionHandler("Email not found in the mailing list.");

            if (!_customMemoryCache.TryGetValue<string>(htmlTemplate, out var template))
            {
                throw new ExceptionHandler("The requested template is not available.");
            }

            var token = GenerateToken(email);

            //Store email temporarily in cache - 30 min
            _customMemoryCache.Set(_unsubscribeCacheKeyPrefix + token, email, TimeSpan.FromMinutes(30));

            //Redirect user to the confirmation page with token
            return Redirect($"https://myservice-x.net/unsubscribe?token={token}");
        }

        /// <summary>
        /// Displays the confirmation page for unsubscription.
        /// This method checks if the provided token is valid and retrieves the associated email address.
        /// </summary>
        /// <param name="token">The token generated for the unsubscription request.</param>
        /// <returns>An <see cref="IActionResult"/> that displays the confirmation message.</returns>
        [HttpGet("confirm")]
        public IActionResult Confirm(string token)
        {
            //Check if the token exists in the cache
            if (!_customMemoryCache.TryGetValue(_unsubscribeCacheKeyPrefix + token, out string email))
                throw new ExceptionHandler("Invalid or expired token.");

            //Display confirmation page with a button to confirm
            return Ok($"Are you sure you want to unsubscribe {email}? Click here to confirm.");
        }

        /// <summary>
        /// Confirms the unsubscription request and removes the email from the mailing list.
        /// This method validates the token, retrieves the associated email, and performs the unsubscription.
        /// </summary>
        /// <param name="token">The token generated for the unsubscription request.</param>
        /// <returns>An <see cref="IActionResult"/> that indicates the success of the unsubscription process.</returns>
        [HttpPost("confirm")]
        public async Task<IActionResult> ConfirmUnsubscribe(string token)
        {
            //Validate token and get the email from cache
            if (!_customMemoryCache.TryGetValue(_unsubscribeCacheKeyPrefix + token, out string email))
                throw new ExceptionHandler("Invalid or expired token.");

            if (_mailingList == null || !_mailingList.Contains(email))
                throw new ExceptionHandler("Email not found in the mailing list.");

            if (_mailingList.Contains(email))
            {
                _mailingList.Remove(email);
            }
            else
            {
                throw new ExceptionHandler("Email not found in the mailing list.");
            }

            // Invalidate the token
            _customMemoryCache.Remove(_unsubscribeCacheKeyPrefix + token);

            await SendWebhookNotification(email);
            await SendEmailNotification(email);

            return Redirect($"https://myservice-x.net/unsubscribe?token={token}&success=true");
        }

        #endregion


        #region << Private methods >>

        /// <summary>
        /// Validates the inputs for the unsubscribe request.
        /// </summary>
        /// <param name="email">The email address of the user requesting unsubscription.</param>
        /// <param name="timestamp">The timestamp indicating when the unsubscribe link was generated.</param>
        /// <exception cref="ExceptionHandler">Throws an ExceptionHandler with error messages for invalid email or timestamp.</exception>
        private void ValidateUnsubscribeInputs(string email, string timestamp)
        {
            // Validate email format
            if (string.IsNullOrWhiteSpace(email) || !Regex.IsMatch(email, EmailPattern))
                throw new ExceptionHandler("The email address provided is invalid. Please use the format firstname.lastname@gmail.com.");

            // Validate the timestamp 
            if (!DateTime.TryParse(timestamp, out DateTime linkTime))
                throw new ExceptionHandler("Invalid timestamp format. Please provide a valid date and time.");

            var currentTime = DateTime.UtcNow;

            // Check if the link timestamp is too old (older than 48 hours) or in the future
            if (linkTime > currentTime || (currentTime - linkTime).TotalHours > 48)
                throw new ExceptionHandler("The link has expired or is invalid. Please request a new unsubscribe link.");
        }

        /// <summary>
        /// Generates a secure token for the specified email address.
        /// The token is created using the SHA-256 hash algorithm and includes the current UTC date and time,
        /// ensuring that each token is unique even for the same email address.
        /// </summary>
        /// <param name="email">The email address for which the token is generated.</param>
        /// <returns>A base64 encoded string representing the generated token.
        private string GenerateToken(string email)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(email + DateTime.UtcNow));
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Sends a notification to a configured webhook URL indicating that a user has unsubscribed.
        /// This method constructs a payload containing the user's email and the status of the unsubscription,
        /// then sends an HTTP POST request to the specified webhook URL.
        /// </summary>
        /// <param name="email">The email address of the user who has unsubscribed.</param>
        /// <returns>A Task representing the asynchronous operation of sending the webhook notification.</returns>
        /// <remarks>
        /// This method will throw an ExceptionHandler if any errors occur during the notification process.
        /// </remarks>
        private async Task SendWebhookNotification(string email)
        {
            var webhookUrl = "https://webhook.site/";
            var payload = new { Email = email, Status = "Unsubscribed" };

            using var client = new HttpClient();

            try
            {
                var response = await client.PostAsJsonAsync(webhookUrl, payload);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMessage = $"Failed to send webhook notification. " +
                               $"Status Code: {response.StatusCode}, Reason: {response.ReasonPhrase}";
                    throw new ExceptionHandler(errorMessage);
                }
            }
            catch (HttpRequestException httpRequestException)
            {
                var errorMessage = $"An error occurred while sending the webhook notification: {httpRequestException.Message}";
                throw new ExceptionHandler(errorMessage);
            }
            catch (Exception ex)
            {
                var errorMessage = $"An unexpected error occurred: {ex.Message}";
                throw new ExceptionHandler(errorMessage);
            }
        }
        /// <summary>
        /// Sends an unsubscription confirmation email to the specified user.
        /// This method constructs an email message with a predefined subject and body,
        /// then sends it using the configured SMTP server.
        /// </summary>
        /// <param name="email">The email address of the user to whom the confirmation email will be sent.</param>
        /// <returns>A Task representing the asynchronous operation of sending the email.</returns>
        /// <remarks>
        /// This method will throw an exception if the email fails to send,
        /// which should be handled by the calling method or the application's global exception handling middleware.
        /// </remarks>
        private async Task SendEmailNotification(string email)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("MyService", "noreply@myservice-x.net"));
                message.To.Add(new MailboxAddress("User", email)); // You can replace this with an admin email if needed
                message.Subject = "Unsubscription Confirmation";
                message.Body = new TextPart("plain")
                {
                    Text = $"Hello,\n\nYou have successfully unsubscribed {{email}}.\n\n"
                };

                using var smtpClient = new MailKit.Net.Smtp.SmtpClient();
                await smtpClient.ConnectAsync("smtp.mailtrap.io", 587, false);  // Use SMTP server credentials
                await smtpClient.AuthenticateAsync("username", "password");    // Replace with username and password
                await smtpClient.SendAsync(message);
                await smtpClient.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                throw new ExceptionHandler($"Failed to send email notification: {ex.Message}");
            }
        }

        #endregion
    }
}
