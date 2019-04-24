

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace GefjonAI
{
    
    public class GefjonAIBot : IBot
    {
        private readonly GefjonAIAccessors _accessors;
        private readonly ILogger _logger;
        public static readonly string LuisKey = "Gefjon";
        public static readonly string QnAMakerKey = "Gefjon_Brain";
        private const string welcomeText = "Hello, I am Gefjon. I can help you find a location, get an image off google, and tell you who someone is.";
        private readonly BotServices _botServices;
        private bool welcomeflag = false; //bool so welcome text wont send twice
        const string subscriptionKey = "b90627b391db4a81a05981b558890f2b"; //subscription key used for the bing search
        const string uriBase = "https://api.cognitive.microsoft.com/bing/v7.0/images/search";


        public GefjonAIBot(ConversationState conversationState, ILoggerFactory loggerFactory, BotServices services)
        {
            if (conversationState == null)
            {
                throw new System.ArgumentNullException(nameof(conversationState));
            }

            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            _accessors = new GefjonAIAccessors(conversationState)
            {
                CounterState = conversationState.CreateProperty<CounterState>(GefjonAIAccessors.CounterStateName),
            };

            _logger = loggerFactory.CreateLogger<GefjonAIBot>();
            _logger.LogTrace("Turn start.");

            _botServices = services ?? throw new System.ArgumentNullException(nameof(services));
            if (!_botServices.LuisServices.ContainsKey(LuisKey))
            {
                throw new System.ArgumentException($"Invalid configuration....");
            }
        }

        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                var response = await _botServices.QnAServices[QnAMakerKey].GetAnswersAsync(turnContext);
                if (response != null && response.Length > 0)
                {
                    await turnContext.SendActivityAsync(response[0].Answer, cancellationToken: cancellationToken);
                }
                else
                {
                    //getting Luis results
                    var recognizerResult = await _botServices.LuisServices[LuisKey].RecognizeAsync(turnContext, cancellationToken);
                    var topIntent = recognizerResult?.GetTopScoringIntent();
                    await TriggerDialog(topIntent.Value.intent.ToString(), turnContext);
                }

            }
            else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                if (!welcomeflag)
                {
                    welcomeflag = true;
                    await turnContext.SendActivityAsync(welcomeText); // Send a welcome message to the user and tell them what actions they may perform to use this bot. 
                }
            }
        }

        private async Task TriggerDialog(string intent, ITurnContext context)
        {
            switch(intent)
            {
                case "GetLocation":
                    await LocationFind(context);
                    break;
                case "GetPerson":
                    await GetPerson(context);
                    break;
                case "GetImage":
                    await GetImage(context);
                    break;
                case "Search":
                    await Search(context);
                    break;
                case "Weather.GetCondition":
                    await GetWeatherConditions(context);
                    break;
                case "Weather.GetForecast":
                    await GetForecast(context);
                    break;
            }
        }

        private async Task GetForecast(ITurnContext context)
        {
            await context.SendActivityAsync("Getting Person...");
        }

        private async Task GetWeatherConditions(ITurnContext context)
        {
            await context.SendActivityAsync("Getting Person...");
        }

        private async Task GetPerson(ITurnContext context)
        {
            await context.SendActivityAsync("Getting Person...");
        }

        private async Task LocationFind(ITurnContext context)
        {
            await context.SendActivityAsync("Finding Location...");
        }

        private async Task GetImage(ITurnContext context)
        {
            SearchResult result = BingSearch(context.Activity.Text); //change context to use only the entity returned
            dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(result.jsonResult); //deserialize JSON response
            var firstJsonObj = jsonObj["value"][0]; //getting first image result

            var attachment = new Attachment
            {
                Name = "image",
                ContentType = "image/png",
                ContentUrl = firstJsonObj["contentUrl"] //content url is the direct link to the image
            };

            var reply = context.Activity.CreateReply();
            reply.Attachments = new List<Attachment>() { attachment };

            await context.SendActivityAsync(reply); // Send the activity to the user.
        }

        static SearchResult BingSearch(string SearchTerm)
        {
            var uriQuery = uriBase + "?q=" + Uri.EscapeDataString(SearchTerm); //creating the search url

            WebRequest request = WebRequest.Create(uriQuery);
            request.Headers["Ocp-Apim-Subscription-Key"] = subscriptionKey; //subscription key used for the api
            HttpWebResponse response = (HttpWebResponse)request.GetResponseAsync().Result;
            string json = new StreamReader(response.GetResponseStream()).ReadToEnd(); //returning web response as json

            var searchResult = new SearchResult() //call to struct to format result
            {
                jsonResult = json,
                relevantHeaders = new Dictionary<String, String>()
            };

            foreach (String header in response.Headers)
            {
                if (header.StartsWith("BingAPIs-") || header.StartsWith("X-MSEdge-"))
                    searchResult.relevantHeaders[header] = response.Headers[header];
            }
            return searchResult;
        }

        private async Task Search(ITurnContext context)
        {
            await context.SendActivityAsync("Searching...");
        }

        struct SearchResult //For formatting JSON response
        {
            public String jsonResult;
            public Dictionary<String, String> relevantHeaders;
        }
    }
}
