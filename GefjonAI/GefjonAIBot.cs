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
using AdaptiveCards;
using AdaptiveCards.Rendering;
using Newtonsoft.Json;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace GefjonAI
{
    
    public class GefjonAIBot : IBot
    {
        #region variables
        private readonly GefjonAIAccessors _accessors;
        private readonly ILogger _logger;
        private readonly BotServices _botServices;

        //keys from .bot file for LUIS and QnA AI's
        public static readonly string LuisKey = "Gefjon";
        public static readonly string QnAMakerKey = "Gefjon_Brain";

        //welcome message
        private const string welcomeText = "Hello, I am Gefjon. I can help you find a location, get an image off google, and tell you who someone is.";
        private bool welcomeflag = false; //bool so welcome text wont send twice

        //image search
        private const string subscriptionKey = "b90627b391db4a81a05981b558890f2b"; //subscription key used for the bing search
        private const string uriBase = "https://api.cognitive.microsoft.com/bing/v7.0/images/search";

        //weather 
        private const string filepath = @"C:\Users\lukeh\Desktop\Comp Sci\GitHub\Gefjon\GefjonAI\card.json"; //file path to the adaptive card json file
        private const string APIXUKey = "0d268c151f8047458e4185904192404"; 
        Weather w = new Weather(); //Weather object stores variables for the adaptive card
        #endregion

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
                /*var response = await _botServices.QnAServices[QnAMakerKey].GetAnswersAsync(turnContext);
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
                }*/

                //
                //Commented out the above for testing the luis methods only 
                //
                var recognizerResult = await _botServices.LuisServices[LuisKey].RecognizeAsync(turnContext, cancellationToken);
                var topIntent = recognizerResult?.GetTopScoringIntent();
                await TriggerDialog(topIntent.Value.intent.ToString(), turnContext);
            }
            else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                
                if (!welcomeflag)
                {
                    
                    await turnContext.SendActivityAsync(welcomeText); // Send a welcome message to the user and tell them what actions they may perform to use this bot. 
                }
                welcomeflag = true;
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
                case "Weather_GetCondition":
                    await GetWeatherConditions(context);
                    break;
                case "Weather_GetForecast":
                    await GetForecast(context);
                    break;
            }
        }

        private async Task GetForecast(ITurnContext context) //might remove to just do current weather conditions
        {
            await context.SendActivityAsync("Getting Person...");
        }

        private async Task GetWeatherConditions(ITurnContext context)
        {
            string searchTerm = "";
            searchTerm = context.Activity.Text; //This is the entire message the user typed in

            UpdateWeather(searchTerm); //updates values
            var cardAttachment = CreateAdaptiveCardAttachment(); //attaches values to card

            var reply = context.Activity.CreateReply();
            reply.Attachments = new List<Attachment>() { cardAttachment };

            await context.SendActivityAsync(reply);
        }

        private void UpdateWeather(string message) //gets weather data from apixu and updates the weather object with values
        {
            string term = message.Replace(" ", "-"); //puts a - between each word for the url

            var urlQuery = "http://api.apixu.com/v1/current.json?key=" + APIXUKey + "&q=" + term; //creates the url

            //sends a request to APIXU and gets a JSON response
            WebRequest request = WebRequest.Create(urlQuery);
            HttpWebResponse response = (HttpWebResponse)request.GetResponseAsync().Result;
            string json = new StreamReader(response.GetResponseStream()).ReadToEnd();

            dynamic jsonObject = JsonConvert.DeserializeObject(json); //dynamic json object allows easier way to get specific value from the json returned

            //Sets the weather object variables
            w.City = jsonObject.location.name;
            w.State = jsonObject.location.region;
            w.CurrentTemp = jsonObject.current.temp_f;
            //w.ImageUrl = jsonObject.current.condition.icon; //.Remove gets rid of the first two characters of the url was //url
            w.ImageUrl = "cdn.apixu.com/weather/64x64/day/143.png";
            w.DateTime = jsonObject.location.localtime; //need to format date and time 
            w.HighTemp = "Wind: " + jsonObject.current.wind_mph + " MPH";
            w.LowTemp = "Feels Like " + jsonObject.current.feelslike_f + "°F";
        }

        private Attachment CreateAdaptiveCardAttachment() 
        {
            var cardObject = AdaptiveCard.FromJson(UpdateCardWeatherValues()).Card;
            
            var adaptiveCardAttachment = new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = cardObject,
            };
            return adaptiveCardAttachment;
        }

        private string UpdateCardWeatherValues()
        {
            string updated;
            var adaptiveCardJson = File.ReadAllText(filepath);

            //puts the information recieved from APIXU into the adaptive card 
            updated = adaptiveCardJson.Replace("City, State", w.City + ", " + w.State);
            updated = updated.Replace("Date", w.DateTime);
            updated = updated.Replace("CurrentTemp", w.CurrentTemp);           
            updated = updated.Replace("HighTemp", w.HighTemp);
            updated = updated.Replace("LowTemp", w.LowTemp);
            updated = updated.Replace("ImageURL", w.ImageUrl); //save current url and figure out other image urls http://messagecardplayground.azurewebsites.net/assets/Mostly%20Cloudy-Square.png

            return updated;
        }

        private async Task GetPerson(ITurnContext context) //not yet implemented
        {
            await context.SendActivityAsync("Getting Person...");
        }

        private async Task LocationFind(ITurnContext context) //not yet implemented
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

        private async Task Search(ITurnContext context) //not yet implemented
        {
            await context.SendActivityAsync("Searching...");
        }

        struct SearchResult //For formatting JSON response
        {
            public String jsonResult;
            public Dictionary<String, String> relevantHeaders;
        }
    }

    public class Weather
    {
        [DataMember]
        public string City { get; set; }
        [DataMember]
        public string State { get; set; }
        [DataMember]
        public string DateTime { get; set; }
        [DataMember]
        public string ImageUrl { get; set; }
        [DataMember]
        public string HighTemp { get; set; }
        [DataMember]
        public string LowTemp { get; set; }
        [DataMember]
        public string CurrentTemp { get; set; }
    }
}
