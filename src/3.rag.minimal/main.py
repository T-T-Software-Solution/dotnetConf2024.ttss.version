import openai
import requests
import json
from pymilvus import connections, utility, db, Collection
from database_operations import initialize_database, reset_database, search_similar_texts, generate_embeddings_openai
from flask import Flask, request, jsonify
import config  # Import the config file for API key and model configuration

app = Flask(__name__)

# Milvus connection details
MILVUS_HOST = 'localhost'
MILVUS_PORT = '19530'

# Database and collection name variables
database_name = "my_database"
collection_name = "thai_text_embeddings"

# Set the OpenAI API key from the config file
openai.api_key = config.OPENAI_API_KEY

# Function to get a response from OpenAI Chat Completion API
def get_chat_completion_response(user_question, context):
    """
    Sends the user question and the retrieved context to OpenAI Chat Completion API.
    """
    url = "https://api.openai.com/v1/chat/completions"
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {config.OPENAI_API_KEY}"  # Set the OpenAI API Key from the config
    }
    messages = [
        {"role": "system", "content": "คุณคือพนักงานของบริษัท ที.ที.ซอฟแวร์ โซลูชั่น จำกัด (T.T.Software Solution Co.,Ltd). กรุณาตอบคำถามเกี่ยวกับบริษัทฯ เป็นภาษาไทย โดยอ้างอิงจาก รายละเอียดที่เกี่ยวข้อง. คุณเป็นผู้ชาย."},
        {"role": "system", "content": f"รายละเอียดที่เกี่ยวข้อง: {context}"},
        {"role": "user", "content": user_question},        
    ]

    data = {
        "model": config.CHAT_COMPLETION_MODEL,  # Use model from config
        "messages": messages,
        "temperature": config.CHAT_COMPLETION_TEMPERATURE  # Use temperature from config
    }

    try:
        response = requests.post(url, headers=headers, data=json.dumps(data))
        response.raise_for_status()
        result = response.json()
        return result['choices'][0]['message']['content']
    except requests.exceptions.HTTPError as http_err:
        print(f"HTTP error occurred: {http_err}")
    except Exception as e:
        print(f"Error generating chat completion with OpenAI: {e}")
    return None

# Function to send a reply to LINE user
def send_line_reply(reply_token, message):
    """
    Sends a reply message back to the user via the LINE Messaging API.
    """
    url = "https://api.line.me/v2/bot/message/reply"
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {config.LINE_CHANNEL_ACCESS_TOKEN}"  # LINE access token from config
    }

    payload = {
        "replyToken": reply_token,
        "messages": [
            {
                "type": "text",
                "text": message
            }
        ]
    }

    response = requests.post(url, headers=headers, data=json.dumps(payload))
    if response.status_code == 200:
        print('Reply sent successfully.')
    else:
        print(f"Failed to send reply: {response.status_code}, {response.text}")

# Flask webhook route to receive messages from LINE
@app.route('/webhook', methods=['POST'])
def webhook():
    body = request.get_json()

    # Check for the 'events' field, which contains message information
    if 'events' in body:
        for event in body['events']:
            if event['type'] == 'message':  # Handle message event
                user_input = event['message']['text'].strip().lower()  # Get the user's message
                reply_token = event['replyToken']  # Extract the replyToken

                # Generate embedding for the user message and find similar context
                embedding = generate_embeddings_openai(user_input, model_name=config.OPENAI_EMBEDDING_MODEL)
                similar_texts = search_similar_texts(collection, embedding, 4)

                # Format the context from the search results
                context = "\n".join([result["Text"] for result in similar_texts])

                if context:
                    # Get the AI response based on context and user input
                    response = get_chat_completion_response(user_input, context)
                    if response:
                        send_line_reply(reply_token, response)  # Send the reply to the user
                    else:
                        send_line_reply(reply_token, "Sorry, I couldn't generate a response.")
                else:
                    send_line_reply(reply_token, "No relevant context found in the database.")

    return jsonify({"status": "success"}), 200

# Start the application
if __name__ == '__main__':
    reset_database()

    # Initialize the database and collection
    collection = initialize_database()

    # Start the Flask app to handle LINE webhooks
    app.run(debug=True, port=5000)
