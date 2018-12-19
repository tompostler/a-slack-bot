using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace a_slack_bot.Functions
{
    public static partial class SlackEntry
    {
        [FunctionName(nameof(ReceiveSlashHelp))]
        public static async Task<HttpResponseMessage> ReceiveSlashHelp(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "receive/slash/help")]HttpRequestMessage req,
            ILogger logger)
        {
            await SR.Init(logger);

            if (Settings.Debug)
                // Trim off the beginning because of AI trying to "help"
                logger.LogInformation("Body: {0}", (await req.Content.ReadAsStringAsync()).Substring(10));

            // Make sure it's a legit request
            if (!await req.IsAuthed(logger))
                return req.CreateErrorResponse(HttpStatusCode.Unauthorized, "Did not match hash.");

            // Return the help
            return req.CreateResponse(HttpStatusCode.OK, new { response_type = "ephemeral", text = SlackEntry.HelpText });
        }

        ////////////////////////////////////////////////////////////////////////////////////////////
        public static string HelpText => $@"```
{C.VersionStr}

A bot constructed for the express purpose of relieving boredom and maybe even providing some useful
functionality. Interaction with the bot is mostly random with numerous easter eggs and various
nuggets of goodness thrown in.

Code, build, and release: https://tompostler.visualstudio.com/public/_git/a-slack-bot

The following are slash commands that can be triggered by the app:

    /asb-help           This helptext
    /asb-response (add|list|remove) ['key' [value]]
                        Add, list or remove custom message responses. Sending the command with no
                        arguments will get you additional helptext.
    /asb-send-as-me [t] Store a user token to be used for replying to slash commands as you instead
                        of as the bot. This uses Slack's legacy token feature because I don't want
                        to deal with the proper OAuth 2.0 Slack flow. If you wish to remove token,
                        pass 'clear' for the token value. Visit
                        https://api.slack.com/custom-integrations/legacy-tokens to generate a token
    /asb-version        Shows the version of the bot.
    /asb-whitelist (add|remove) 'thing-to-whitelist'
                        Based on the thing-to-whitelist, will update whatever is necessary to make
                        it happen. An example right now is needing to whitelist the /blackjack
                        command in a channel.
    /balance            Shows you your current ¤ balance.
    /balances           Shows all everyone's ¤ balance.
    /blackjack          Starts a threaded game of blackjack. Needs to be whitelisted in a channel.
    /disapprove         Sends ಠ_ಠ to the channel
    /flip [text]        Echoes your message, followed by (╯°□°)╯︵ ┻━┻
    /guess [num]        Play a single-number guessing game where the number has to be in the
                        interval (0, balance]. Often rewards those whose balance is less than 100.
                        Needs to be whitelisted in a channel.
    /guid               Generate a guid
    /spaces [text]      Echoes your message, with spaces inserted betwix letters
" +
////////////////////////////////////////////////////////////////////////////////////////////////////
$@"
Other things that you can access or just kind of happen sometimes:

    @{SR.U.BotUser.profile.real_name}{new string(' ', 18 - SR.U.BotUser.profile.real_name.Length)} Will have the bot respond to you with an ACK message
" +
////////////////////////////////////////////////////////////////////////////////////////////////////
"```";
    }
}
