// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace GefjonAI
{
    
    public class GefjonAIBot : IBot
    {
        private readonly GefjonAIAccessors _accessors;
        private readonly ILogger _logger;
        public static readonly string LuisKey = "Gefjon";
        private const string welcomeText = "Hello, I am Gefjon. I can help you find a location and get an image off google";
        private readonly BotServices _botServices;

        private string googleurl = "https://www.google.com/search?q=";
        private string urlend = "&tbm=isch";

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
                //might delete turn counter stuff vvv
                var state = await _accessors.CounterState.GetAsync(turnContext, () => new CounterState());  // Get the conversation state from the turn context.
                state.TurnCount++;  // Bump the turn count for this conversation.
                await _accessors.CounterState.SetAsync(turnContext, state); // Set the property using the accessor.
                await _accessors.ConversationState.SaveChangesAsync(turnContext); // Save the new turn count into the conversation state.

                //getting Luis results
                var recognizerResult = await _botServices.LuisServices[LuisKey].RecognizeAsync(turnContext, cancellationToken);
                var topIntent = recognizerResult?.GetTopScoringIntent();
                TriggerDialog(topIntent.Value.intent.ToString(), turnContext);
                //if (topIntent != null && topIntent.HasValue && topIntent.Value.intent != "None")
                //{
                  //  await turnContext.SendActivityAsync($"==>LUIS Top Scoring Intent: {topIntent.Value.intent}, Score: {topIntent.Value.score}\n");
                //}
                //else
                //{
                //    var msg = @"No LUIS intents were found.";
                //    await turnContext.SendActivityAsync(msg);
                //}

            }
            else if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
            {
                // Send a welcome message to the user and tell them what actions they may perform to use this bot. 
               await turnContext.SendActivityAsync(welcomeText); //this sends twice see docs here:https://docs.microsoft.com/en-us/azure/bot-service/bot-builder-send-welcome-message?view=azure-bot-service-4.0&tabs=csharp
            }
        }

        private async Task TriggerDialog(string intent, ITurnContext context)
        {
            switch(intent)
            {
                case "LocationFinder":
                    await LocationFind(context);
                    break;
                case "Greeting":
                    await Greeting(context);
                    break;
                case "Search":
                    await SearchFor(context, context.Activity.Text);
                    break;
                case "Stop":
                    await Stop(context);
                    break;
            }
        }

        private async Task Greeting(ITurnContext context)
        {
            await context.SendActivityAsync("Greetings");
        }

        private async Task LocationFind(ITurnContext context)
        {
            await context.SendActivityAsync("Finding Location...");
        }

        private async Task SearchFor(ITurnContext context, string message)
        {
            

            string url = googleurl + message + urlend;

            /*string html = GetHTML(url);
            List<string> urls = GetUrls(html);
            var rnd = new Random();

            int randomUrl = rnd.Next(0, urls.Count - 1);

            string luckyUrl = urls[randomUrl];

            byte[] image = GetImage(luckyUrl);
            using (var ms = new MemoryStream(image))
            {
                
            }*/

            var attachment = new Attachment
            {
                ContentUrl = url
            };

            var reply = context.Activity.CreateReply();
            reply.Attachments = new List<Attachment>() { attachment };

            // Send the activity to the user.
            await context.SendActivityAsync(reply);
        }

        //Might delete these vvv used for getting image windows form
        private string GetHTML(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Accept = "text/html, application/xhtml+xml, */*";
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko";
            string data = "";

            var response = (HttpWebResponse)request.GetResponse();

            using (Stream dataStream = response.GetResponseStream())
            {
                if (dataStream == null)
                    return "";
                using (var sr = new StreamReader(dataStream))
                {
                    data = sr.ReadToEnd();
                }
            }
            return data;
        }
        private List<string> GetUrls(string html)
        {
            var urls = new List<string>();

            int ndx = html.IndexOf("\"ou\"", StringComparison.Ordinal);

            while (ndx >= 0)
            {
                ndx = html.IndexOf("\"", ndx + 4, StringComparison.Ordinal);
                ndx++;
                int ndx2 = html.IndexOf("\"", ndx, StringComparison.Ordinal);
                string url = html.Substring(ndx, ndx2 - ndx);
                urls.Add(url);
                ndx = html.IndexOf("\"ou\"", ndx2, StringComparison.Ordinal);
            }
            return urls;
        }
        private byte[] GetImage(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            var response = (HttpWebResponse)request.GetResponse();

            using (Stream dataStream = response.GetResponseStream())
            {
                if (dataStream == null)
                    return null;
                using (var sr = new BinaryReader(dataStream))
                {
                    byte[] bytes = sr.ReadBytes(100000000);

                    return bytes;
                }
            }
        }

        //probably dont need stop intent vvv
        private async Task Stop(ITurnContext context)
        {
            await context.SendActivityAsync("Stoping.");
        }
    }
}
