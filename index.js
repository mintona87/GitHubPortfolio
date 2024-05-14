//Author N.Minton
const { BedrockRuntimeClient, InvokeModelCommand } = require("@aws-sdk/client-bedrock-runtime");
const { AmazonKnowledgeBaseRetriever } = require("@langchain/community/retrievers/amazon_knowledge_base");

// Initialize BedrockRuntimeClient for invoking models
const client = new BedrockRuntimeClient({ region: "us-east-1" });

exports.handler = async (event) => {
    console.log('Event received:', JSON.stringify(event));
    const userInput = event.inputTranscript;
    console.log('User input:', userInput);

    // Initialize the Langchain retriever for the knowledge base
    const retriever = new AmazonKnowledgeBaseRetriever({
        topK: 10, 
        knowledgeBaseId: "REDACTED", 
        region: "us-east-1",
    });

    try {
        console.log(`Querying the knowledge base for user input: "${userInput}"`);
        const docs = await retriever.getRelevantDocuments(userInput);
        console.log(`Retrieved ${docs.length} documents from the knowledge base successfully.`);
        
        const bedrockPrompt = `
        Human: ${userInput}
        bedrockprompt = "Given the list of following reference passages: ${docs.map(doc => `- ${doc.pageContent}`).join("\n")}"
        References:
        ${docs.map(doc => `- ${doc.pageContent}`).join("\n")}
        bedrockprompt += "\n And the following question: ${userInput}"
        bedrockprompt += "Identify the best response you can extract from the knowledge base documents. Summarize its content into a few sentences appropriate for a chatbot response. Act as if you already knew the information, without revealing to the human that you looked up the answer. If you don't have any additional information about the Human's request, simply say so. Do not direct the user to contact Transurban's customer service for further inquiries but ask them if they can provide more context to their question or if there is anything else you can assist with."
        bedrockprompt += "If there is not enough information given in ${userInput} to find a matching resource in the passages, reply with: I'm sorry, I didn't quite catch that. Could you please rephrase your question or try a different query?"
        Assistant: `;


        console.log('Prompt for Bedrock:', bedrockPrompt);

        const bedrockInput = {
            modelId: "anthropic.claude-v2:1", 
            contentType: "application/json",
            accept: "*/*",
            body: JSON.stringify({
                prompt: bedrockPrompt,
                max_tokens_to_sample: 400,
                temperature: 0.75,
              //  topP: 0.9,
              //  stopSequences: ["\n"],
            }),
        };

        console.log('Invoking Bedrock model with input:', JSON.stringify(bedrockInput));
        const data = await client.send(new InvokeModelCommand(bedrockInput));
        const jsonString = Buffer.from(data.body).toString('utf8');
        const parsedData = JSON.parse(jsonString);

        console.log('Bedrock response:', JSON.stringify(parsedData));
        let textResponse;
          if (parsedData && parsedData.completion) {
              textResponse = parsedData.completion;
          } else {
              textResponse = "Sorry, I couldn't process your request.";
          }


        console.log('Text response from Bedrock:', textResponse);

        // Return the response to Lex
        return {
            sessionState: {
                dialogAction: {
                    type: "Close",
                    fulfillmentState: "Fulfilled",
                },
                intent: {
                    name: "FallbackIntent",
                    state: "Fulfilled",
                },
            },
            messages: [{ 
                contentType: "PlainText", 
                content: textResponse,
            }],
        };
    } catch (error) {
        console.error('Error:', error);
        // Return an error message to Lex
        return {
            sessionState: {
                dialogAction: {
                    type: "Close",
                    fulfillmentState: "Failed",
                },
                intent: {
                    name: "FallbackIntent",
                    state: "Failed",
                },
            },
            messages: [{ 
                contentType: "PlainText", 
                content: "Sorry, I was unable to find an answer. Please try again.",
            }],
        };
    }
};

