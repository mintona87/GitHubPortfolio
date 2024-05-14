import OpenAI from 'openai';
import fetch from 'node-fetch';

const openai = new OpenAI({
  apiKey: process.env.OPENAI_API_KEY,
});

console.log('OpenAI SDK initialized.');

// Helper function to check if the input contains forbidden topics
function isInputValid(input) {
  const forbiddenTopics = ['gold', 'commodities', 'aux', 'COMPETITOR_NAME'];
  return !forbiddenTopics.some(topic => input.toLowerCase().includes(topic));
}

// Helper function to sanitize input to prevent prompt injection
function sanitizeInput(input) {
  let modifiedInput = input.replace(/\bLST\b/gi, 'L**** S*** T***');
  modifiedInput = "In the context of the project: " + modifiedInput;
  const sanitizedInput = modifiedInput.replace(/[\r\n]/g, ' ').trim();
  if (sanitizedInput.length > 1500) {
    return sanitizedInput.substring(0, 1500);
  }
  return sanitizedInput;
}

// Helper function to remove source references
function removeSourceReferences(input) {
  return input.replace(/【\d+:\d+†[^】]+】/g, '');
}

// Function to validate model's output against inappropriate content
function validateResponse(response) {
  const inappropriateContents = ['unrelated financial advice', 'speculative content', 'gold', 'iau', 'commodities', 'COMPETITOR_NAME', 'liquid staking tokens'];
  return inappropriateContents.every(content => !response.toLowerCase().includes(content));
}

// Function to fetch trusted data from a vector store
async function fetchTrustedData(vectorStoreId) {
  const url = `https://api.openai.com/v1/vector_stores/${vectorStoreId}`;
  const headers = {
    'Authorization': `Bearer ${process.env.OPENAI_API_KEY}`,
    'Content-Type': 'application/json',
    'OpenAI-Beta': 'assistants=v2'
  };

  console.log('Fetching trusted data from:', url);
  console.log('Using headers:', headers);

  try {
    const response = await fetch(url, { headers: headers });
    const data = await response.json();

    if (!response.ok) {
      console.error('Failed to fetch trusted data:', data);
      throw new Error(`HTTP error! status: ${response.status}`);
    }
    
    console.log('Received trusted data:', data);
    return data;
  } catch (error) {
    console.error('Error during fetch operation:', error);
    return null;
  }
}

async function retrieveDocuments(vectorStoreId) {
  const url = `https://api.openai.com/v1/vector_stores/${vectorStoreId}/files`;
  const headers = {
    'Authorization': `Bearer ${process.env.OPENAI_API_KEY}`,
    'Content-Type': 'application/json',
    'OpenAI-Beta': 'assistants=v2'
  };

  try {
    const response = await fetch(url, { headers: headers });
    const data = await response.json();
    if (!response.ok) {
      console.error('Failed to retrieve documents:', data);
      throw new Error(`HTTP error! status: ${response.status}`);
    }
   
    return data.files || []; 
  } catch (error) {
    console.error('Failed to retrieve documents:', error);
    return []; 
  }
}

async function createThreadAndPostMessage(messageContent, vectorStoreId) {
  const threadResponse = await openai.beta.threads.create({
    tool_resources: {
      file_search: {
        vector_store_ids: [vectorStoreId]
      }
    }
  });
  console.log('Thread created:', threadResponse.id);

  const messageResponse = await openai.beta.threads.messages.create(threadResponse.id, {
    role: "user",
    content: messageContent,
  });
  console.log('Message added to thread:', messageResponse.id);

  return threadResponse.id;
}

async function runThreadAndGetResponse(threadId, assistantId, instructions, vectorStoreId) {
  const documents = await retrieveDocuments(vectorStoreId);
  const contextualInstructions = `${instructions} Context: ${documents.map(doc => doc.summary).join(' ')}`;

  // Create a message in the thread with the full instructions including context
  const messageResponse = await openai.beta.threads.messages.create(threadId, {
    role: "user",
    content: contextualInstructions
  });
  console.log('Message added to thread:', messageResponse.id);

  // Initiate a run to process the message
  const runResponse = await openai.beta.threads.runs.create(threadId, {
    assistant_id: assistantId
  });
  console.log(`Run initiated with ID: ${runResponse.id}`);

  let runStatusResponse;
  do {
    await new Promise(resolve => setTimeout(resolve, 5000));
    runStatusResponse = await openai.beta.threads.runs.retrieve(threadId, runResponse.id);
  } while (runStatusResponse.status !== 'completed');

  console.log(`Run completed with status: ${runStatusResponse.status}`);

  const messagesResponse = await openai.beta.threads.messages.list(threadId);
  const assistantMessages = messagesResponse.data.filter(msg => msg.role === 'assistant');
  const assistantResponses = assistantMessages.map(msg => msg.content.map(content => content.text?.value || '').join(' ')).join('\n');

  // Remove unwanted patterns from the response
  const sanitizedResponse = removeSourceReferences(assistantResponses);

  if (!validateResponse(sanitizedResponse)) {
    return "The response was flagged for containing inappropriate content.";
  }

  // Verify facts against trusted data
  const trustedData = await fetchTrustedData(vectorStoreId);
  if (!trustedData || (trustedData.documents && !trustedData.documents.some(doc => sanitizedResponse.includes(doc.summary)))) {
    console.log("Trusted data:", trustedData);
    return "The response failed verification against trusted data.";
  }

  return sanitizedResponse;
}

export async function handler(event) {
  const responseHeaders = {
    'Access-Control-Allow-Origin': '*', 
    'Access-Control-Allow-Headers': 'Content-Type',
    'Access-Control-Allow-Methods': 'OPTIONS,POST',
    'Content-Type': 'application/json',
  };

  if (event.httpMethod === "OPTIONS") {
    console.log('Handling OPTIONS request for CORS preflight.');
    return { statusCode: 200, headers: responseHeaders, body: JSON.stringify({}) };
  }

  if (event.httpMethod === "POST") {
    try {
      console.log("Processing POST request:", event.body);
      const requestBody = JSON.parse(event.body);
      const userMessage = sanitizeInput(requestBody.message);
      if (!userMessage || !isInputValid(userMessage)) {
        console.log("Bad Request: Message is required or contains forbidden topics");
        return { statusCode: 400, headers: responseHeaders, body: JSON.stringify({ message: "Bad Request: Message is required or contains forbidden topics" }) };
      }

      const vectorStoreId = 'YOUR_VECTOR_STORE_ID'; 
      const assistantId = "YOUR_ASSISTANT_ID";
      const instructions = 'You are a specialized bot providing detailed information and guidance about a blockchain protocol. Your primary role is to answer questions and provide insights related to various aspects of the protocol, including assets, **** pools, *******, governance, and staking. System Instructions: Clear Input and Output Delimitation: Use Markdown formatting: Clearly separate questions, commands, and responses using Markdown to enhance readability. Detail request prompt: Explicitly ask users for specific details when queries are vague or incomplete, ensuring the query can be fully understood before processing. Integration and Use of Document Search Tools: Embeddings-based document retrieval: Implement embeddings-based search to dynamically extract information relevant to user queries from uploaded documents, ensuring responses are precisely tailored to the query content. Automate data retrieval: Systematically extract and reference content during user interactions to provide real-time, contextually relevant information. Task Decomposition: Simplify complex requests: Automatically parse and break down complex inquiries into manageable tasks that can be individually addressed to ensure comprehensive responses. Sequential task processing: Handle each subtask in sequence to build a coherent overall response, enhancing the logical flow of information. Adjustment of Response Length: Manage response length: Standardize responses to concise bullet points for clarity and brevity, expanding into detailed explanations only upon explicit request. Expandable response sections: Allow users to request more detailed information if needed, which can be provided in expandable text sections within the response. Reasoning Time Management: Processing time indicator: Include a brief processing status update for complex queries to manage expectations and maintain user engagement during longer reasoning times. Reference Text Utilization with Citation: Direct citation from documents: When possible, directly quote and cite the reference text from uploaded documents in the responses to ensure the accuracy and reliability of information. Documentation-derived answers: Notify users when responses are based directly on provided documentation, enhancing transparency and trust. If you are unable to assist the user due to the lack of information in your knowledge base, refer the user to open a support ticket in the support channel. Avoid discussing topics unrelated to the protocol, making financial or investment advice, engaging in speculative discussions about cryptocurrency values, or giving legal advice. Steer clear of any content that might compromise the security and privacy of blockchain networks and individual users, and adhere strictly to ethical guidelines and OpenAI's use case policies. Forbidden Topics include gold, commodities, COMPETITOR_NAME.';

      const threadId = await createThreadAndPostMessage(userMessage, vectorStoreId);
      const assistantResponse = await runThreadAndGetResponse(threadId, assistantId, instructions, vectorStoreId);
      if (assistantResponse.startsWith("The response was flagged")) {
        return { statusCode: 200, headers: responseHeaders, body: JSON.stringify({ message: assistantResponse }) };
      }
      return { statusCode: 200, headers: responseHeaders, body: JSON.stringify({ message: assistantResponse }) };
    } catch (error) {
      console.error("Error processing POST request:", error);
      return { statusCode: 500, headers: responseHeaders, body: JSON.stringify({ message: 'Internal Server Error' }) };
    }
  } else {
    return { statusCode: 405, headers: responseHeaders, body: JSON.stringify({ message: "Method Not Allowed" }) };
  }
}
