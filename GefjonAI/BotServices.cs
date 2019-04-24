using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Configuration;

namespace GefjonAI
{
    public class BotServices
    {
        public BotServices(BotConfiguration botConfiguration)
        {
            foreach (var service in botConfiguration.Services)
            {
                switch (service.Type)
                {
                    case ServiceTypes.Luis:
                    {
                        var luis = (LuisService)service;
                        if (luis == null)
                        {
                            throw new InvalidOperationException("The LUIS service is not configured correctly in your '.bot' file.");
                        }

                        var app = new LuisApplication(luis.AppId, luis.AuthoringKey, luis.GetEndpoint());
                        var recognizer = new LuisRecognizer(app);
                        this.LuisServices.Add(luis.Name, recognizer);
                        break;
                    }
                    case ServiceTypes.QnA:
                    {
                        // Create a QnA Maker that is initialized and suitable for passing
                        // into the IBot-derived class (QnABot).
                        var qna = service as QnAMakerService;

                        var qnaEndpoint = new QnAMakerEndpoint()
                        {
                            KnowledgeBaseId = qna.KbId,
                            EndpointKey = qna.EndpointKey,
                            Host = qna.Hostname,
                        };

                        var qnaMaker = new QnAMaker(qnaEndpoint);
                        QnAServices.Add(qna.Name, qnaMaker);
                        break;
                    }
                }
            }

        }

        // Gets the set of LUIS Services used. LuisServices is represented as a dictionary.  
        public Dictionary<string, LuisRecognizer> LuisServices { get; } = new Dictionary<string, LuisRecognizer>();

        public Dictionary<string, QnAMaker> QnAServices { get; } = new Dictionary<string, QnAMaker>(); //Not sure if QnAMaker will work
    }
}
