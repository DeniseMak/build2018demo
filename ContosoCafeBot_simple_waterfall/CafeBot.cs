﻿using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Microsoft.Bot.Builder.Dialogs;
using ContosoCafeBot.Dialogs;
using Newtonsoft.Json.Linq;
using Microsoft.Bot.Builder.Ai.LUIS;
using Microsoft.Bot.Builder.Ai.QnA;


namespace ContosoCafeBot
{
    public class CafeBot : IBot
    {
        private DialogSet _dialogs;
        public CafeBot()
        {
            _dialogs = new DialogSet();

            _dialogs.Add("WhoAreYou", new WhoAreYou());
            _dialogs.Add("BookTable", new BookTable());

        }
        public async Task OnTurn(ITurnContext context)
        {
            //TODO: is this the right way to handle cards?
            string utterance = context.Activity.Text;
            JObject cardData = (JObject)context.Activity.Value;
            if (cardData != null && cardData.Property("intent") != null) utterance = cardData["utterance"].ToString();

            var userState = context.GetUserState<CafeBotUserState>();
            var conversationState = context.GetConversationState<CafeBotConvState>();

            switch (context.Activity.Type)
            {
                case ActivityTypes.ConversationUpdate:
                    var newUserName = context.Activity.MembersAdded[0].Name;
                    if (!string.IsNullOrWhiteSpace(newUserName) && newUserName != "Bot")
                    {
                        await context.SendActivity($"Hello {newUserName}! I'm the Cafe bot!");
                        // remember the user's name
                        userState.name = newUserName;

                        userState.sendCards = true;

                        await context.SendActivity("I can help you find contoso cafe locations, book a table and answer questions about Contoso cafe!");

                        // send a welcome card
                        if(userState.sendCards) await context.SendActivity(CreateCardResponse(context.Activity, createWelcomeCardAttachment()));
                    }
                    break;
                case ActivityTypes.Message:
                    
                    // create dialogContext
                    DialogContext dc = _dialogs.CreateContext(context, conversationState);

                    if(utterance == "start over") {
                            //restart the conversation
                            await context.SendActivity("Sure.. Let's start over");      
                            dc.EndAll();  
                    } else {
                        // continue with any active dialogs
                        await dc.Continue();
                    }
                    if(!context.Responded)
                    {
                        // top level dispatch
                        switch (utterance)
                        {
                            case "hi":
                                await context.SendActivity("Hello, I'm the contoso cafe bot. How can I help you?");
                                if (userState.sendCards) await context.SendActivity(CreateCardResponse(context.Activity, createWelcomeCardAttachment()));
                                break;
                            case "book table":
                                // await context.SendActivity("I'm still learning to book a table!");
                                // start waterfall dialog for table booking
                                await dc.Begin("BookTable");
                                break;
                            case "who are you?":
                                // await context.SendActivity("I'm the cafe bot!");
                                // start waterfall dialog for who are you? 
                                await dc.Begin("WhoAreYou");
                                break;
                            default:
                                await getQnAResult(context);
                                break;
                        }
                    } 
                    break;
            }
        }
        private async Task getQnAResult(ITurnContext context)
        {
            var qEndpoint = new QnAMakerEndpoint()
            {
                Host = "https://contosocafeqnab8.azurewebsites.net/qnamaker",
                EndpointKey = "0fa7f711-6a82-4155-9cf9-5c8168967df6",
                KnowledgeBaseId = "dfa449da-1fb7-449e-b753-53af1b1f7b5b"
            };
            var qOptions = new QnAMakerOptions()
            {
                ScoreThreshold = 0.4F,
                Top = 1
            };
            var qnamaker = new QnAMaker(qEndpoint, qOptions);
            QueryResult[] qResult = await qnamaker.GetAnswers(context.Activity.Text);
            if (qResult.Length == 0)
            {
                await context.SendActivity("Sorry, I do not understand.");
                await context.SendActivity("You can say hi or book table or find locations");
            }
            else
            {
                await context.SendActivity(qResult[0].Answer);
            }
        }
        private Activity CreateCardResponse(Activity activity, Attachment attachment)
        {
            var response = activity.CreateReply();
            response.Attachments = new List<Attachment>() { attachment };
            return response;
        }
        // Methods to generate welcome card
        private Attachment createWelcomeCardAttachment()
        {
            var adaptiveCard = File.ReadAllText(@".\Assets\cards\welcomeCard.json");
            return new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(adaptiveCard)
            };
        }
    }    
}
