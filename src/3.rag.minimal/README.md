# Chatbot Integration with OpenAI and LINE Messaging API

This repository contains the implementation of a chatbot using OpenAI's GPT model integrated with the LINE Messaging API for a Thai language application. The chatbot is hosted using Flask and Milvus for text embeddings storage and retrieval.

## Prerequisites

1. **Python** 3.7 or later
2. **Milvus** 2.0 or later
3. **Flask** 2.0 or later
4. **OpenAI API Key** for accessing GPT models
5. **LINE Channel Access Token** for sending messages via LINE

## Setup Instructions

### Step 1: Clone the Repository

Clone the repository to your local machine:

```bash
git clone https://github.com/nakornttss/minimalllmdemo.git
cd minimalllmdemo
```

### Step 2: Install Dependencies

Install the required dependencies using the following command:

```bash
pip install -r requirements.txt
```

### Step 3: Configure the `config.py`

Edit the `config.py` file with your OpenAI API key, LINE Channel Access Token, and other configurations:

```python
# config.py
OPENAI_API_KEY = "your_openai_api_key"
OPENAI_EMBEDDING_MODEL = "text-embedding-3-large"  # You can use a smaller model like "text-embedding-3-small"
CHAT_COMPLETION_MODEL = "gpt-4o-mini"
CHAT_COMPLETION_TEMPERATURE = 0.7
LINE_CHANNEL_ACCESS_TOKEN = "your_line_channel_access_token"
```

### Step 4: Running the Flask Application

Run the Flask application to start the webhook server:

```bash
python main.py
```

The server will start on `http://localhost:5000`.

### Step 5: Expose Localhost using ngrok

Download and install ngrok from [ngrok website](https://ngrok.com/download).

Start ngrok to expose your local Flask application:

```bash
ngrok http 5000
```

Copy the HTTPS URL provided by ngrok, which looks like `https://your-ngrok-url.ngrok.io`.

### Step 6: Set Up LINE Webhook

1. Go to the LINE Developer Console.
2. Set the webhook URL to `https://your-ngrok-url.ngrok.io/webhook`.
3. Enable the webhook and messaging API.

### Step 7: Start Milvus with Docker Compose

If you are using Docker to run Milvus, start the services using Docker Compose:

```bash
docker-compose up -d
```

This command will start Milvus and its dependencies in the background. Ensure that the Milvus service is running on `localhost:19530` before proceeding.

### Project Structure

- **config.py**: Configuration file for API keys and models.
- **database_operations.py**: Contains functions to initialize, reset, and interact with the Milvus database.
- **docker-compose.yml**: Docker configuration file for setting up the required services.
- **main.py**: Main entry point for the Flask application handling the LINE webhook.
- **README.md**: Documentation for the project.
- **requirements.txt**: List of required Python packages.
- **texts.py**: Contains an array of `initial_texts` for demonstration of Retrieval-Augmented Generation (RAG).
- **webhook.py**: Flask application file specifically for webhook processing and response generation.

### Functionality Overview

- **get_chat_completion_response(user_question, context)**: Sends the user question along with retrieved context to OpenAI's Chat Completion API and returns a response.
- **send_line_reply(reply_token, message)**: Sends a reply message back to the user via the LINE Messaging API.
- **webhook()**: Flask route to receive messages from LINE and respond using OpenAI.

### Database Operations

- **initialize_database()**: Sets up the Milvus database and collection.
- **reset_database()**: Clears and resets the Milvus collection.
- **generate_embeddings_openai(text, model_name)**: Generates text embeddings using OpenAI's models.
- **search_similar_texts(collection, embedding, top_k)**: Searches for similar texts in the Milvus collection based on provided embeddings.

### Example

To test the chatbot using LINE and ngrok:

1. Add your LINE bot as a friend.
2. Open a chat with your bot and send a message, for example: "What services does T.T. Software provide?"
3. The bot will respond using the context retrieved from the Milvus database and OpenAI's GPT model.

**Local Testing with ngrok**:
- Ensure your Flask server is running locally (`python main.py`).
- Start ngrok and note the HTTPS URL.
- Set the webhook URL in the LINE Developer Console to the ngrok URL (e.g., `https://your-ngrok-url.ngrok.io/webhook`).
- Send messages to your LINE bot and see the real-time responses in the chat.

## Troubleshooting

- **Milvus Connection Issues**: Ensure Milvus is running on `localhost:19530`.
- **OpenAI API Errors**: Check your API key and usage limits.
- **LINE Messaging API Issues**: Verify the channel access token and webhook URL.

## License

This project is licensed under the MIT License.
