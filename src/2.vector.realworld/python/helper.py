from pythainlp.util import normalize
from pythainlp.tokenize import word_tokenize
import requests  # Import the requests library for HTTP requests
import json      # Import the json library for handling JSON data
from pymongo import MongoClient
from pymongo.operations import SearchIndexModel
import requests
import json
import config

# Improved function to preprocess Thai text using deepcut tokenizer
def preprocess_text(text: str) -> str:
    # Normalize text (e.g., remove extra spaces, standardize characters)
    text = normalize(text)

    # Tokenize the text using deepcut for better segmentation
    tokens = word_tokenize(text, engine="deepcut")  # Switch to deepcut tokenizer

    # Remove empty tokens and join them back with a single space
    tokens = [token for token in tokens if token.strip()]

    # Optional: Remove commas if necessary
    tokens = [token for token in tokens if token != ',']

    # Return preprocessed text
    return " ".join(tokens)

# Function to generate embeddings using OpenAI API via HTTP POST request
def generate_embeddings_openai(text, model_name):
    """
    Generates embeddings (vectors) using the OpenAI API via an HTTP POST request.
    """
    url = "https://api.openai.com/v1/embeddings"
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {config.OPENAI_API_KEY}"  # Load API key from config
    }
    data = {
        "input": text,
        "model": model_name  # Use the passed model name
    }

    try:
        response = requests.post(url, headers=headers, data=json.dumps(data))
        response.raise_for_status()  # Raise an exception for HTTP errors
        result = response.json()

        # Extract and return the embedding from the response
        embeddings = result['data'][0]['embedding']
        return embeddings
    except requests.exceptions.HTTPError as http_err:
        print(f"HTTP error occurred: {http_err}")
    except Exception as e:
        print(f"Error generating embeddings with OpenAI: {e}")
        return None
    
# Function to create a blank collection
def create_blank_collection(collection_name: str):
    client = MongoClient(config.MONGO_URI)
    db = client[config.MONGO_DB]

    # Check if the collection exists, and drop it if it does
    if collection_name in db.list_collection_names():
        print(f"Dropping collection '{collection_name}'...")
        db.drop_collection(collection_name)
        print(f"Collection '{collection_name}' dropped.")

    # Create a new blank collection
    collection = db.create_collection(collection_name)
    print(f"Created a blank collection '{collection_name}'.")

    return collection

# Function to drop and create a specific vector index
def drop_and_create_vector_index(collection, index_field: str, index_name: str):
    # Check if the index exists and drop it if it does
    indexes = collection.index_information()
    if index_name in indexes:
        print(f"Dropping existing index '{index_name}'...")
        collection.drop_index(index_name)
        print(f"Dropped existing index '{index_name}'.")

    # Check if the collection has no documents and insert a placeholder if needed
    if collection.estimated_document_count() == 0:
        placeholder_embedding = [0.0] * 3072
        collection.insert_one({"_id": "placeholder", "embedding": placeholder_embedding, "text": "placeholder"})
        print(f"Inserted placeholder document into collection '{collection.name}' as it was empty.")

    # Define the vectorSearch index for the collection using SearchIndexModel
    search_index_model = SearchIndexModel(
        definition={
            "fields": [
                {
                    "type": "vector",
                    "numDimensions": 3072,  # Number of dimensions for text-embedding-3-large
                    "path": index_field,  # Field to index for vector search
                    "similarity": "cosine"  # Options: "euclidean", "cosine", or "dotProduct"
                }
            ]
        },
        name=index_name,  # Index name
        type="vectorSearch"  # Index type as vectorSearch
    )

    # Create the vector search index using the defined model
    try:
        result = collection.create_search_index(model=search_index_model)
        print(f"Collection '{collection.name}' has been reset with a vectorSearch index. Index ID: {result}")
    except Exception as e:
        print(f"Failed to create vectorSearch index: {e}")

    # Remove the placeholder document after creating the index if it was inserted
    collection.delete_one({"_id": "placeholder"})
    print("Removed placeholder document after index creation.")    

# Function to generate suggestions using OpenAI ChatCompletion REST API
def generate_suggestions_openai(att_start_end):
    url = "https://api.openai.com/v1/chat/completions"
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {config.OPENAI_API_KEY}"
    }
    data = {
        "model": "gpt-4o-mini",  # Update to the correct model name as needed
        "messages": [
            {"role": "system", "content": "You are an expert in Thai travel schedules. Your job is to extract specific information from a given schedule description."},
            {"role": "user", "content": (
                f"Extract the following information from the given schedule description in Thai:\n"
                f"Description: {att_start_end}\n"
                f"1. EXT_SUGGEST_MONTH: Show the months in Thai, separate multiple months by comma."
                f"Put ทั้งปี and do not put any month if suggested for the entire year. \n"
                f"Please remember that you need to understand in correct way \n"
                f"For example, ทุกวัน 06.00 18.00 น.ช่วงเดือนพฤศจิกายน มกราคม, It mean เปิดตั้งแต่ 06.00 จนถึง 18.00 น. ตั้งแต่ พฤศจิกายน จนถึง มกราคม\n"
                f"2. EXT_SUGGEST_DAY: Show the days of the week in Thai, separate multiple days by comma. "
                f"Put ทุกวัน and do not put any day if suggest for the entire week.\n"
                f"Please remember that you need to understand in correct way \n"
                f"For example, 07.00  18.30 น.จันทร์ อาทิตย์, It mean เปิดตั้งแต่ 07.00 จนถึง 18.30 น. ตั้งแต่ จันทร์ จนถึง อาทิตย์\n"
                f"For example, Pattaya Dolphinarium เปิดให้บริการทุกวัน (ยกเว้นวันพุธ), It mean ตั้งแต่ จันทร์, อังคาร, พฤหัส, ศุกร์, เสาร์, อาทิตย์\n"
                f"For example, จันทร์   เสาร์, It mean เปิด จันทร์, อังคาร, พุธ, พฤหัส, ศุกร์, เสาร์\n"
                f"3. EXT_SUGGEST_TIME: Classify suggest time in Thai as เช้า, บ่าย, เย็นหรือค่ำ, กลางคืน. Separate multiple suggest times by comma.\n"
                f"Put ตลอดเวลา and do not put any time if suggest for the 24 hour.\n"
                f"Please remember that you need to understand in correct way \n"
                f"For example, เปิดทุกวันอาทิตย์ เวลา 08.00 16.00 น., It mean เปิดทุกวันอาทิตย์ เวลา ตั้งแต่ 08.00 จนถึง 16.00 น.\n"
                f"For example, 05.00 18.00, It mean เปิดตั้งแต่ 08.00 จนถึง 18.00 น.\n"
                f"For example, 09.30-16.00, It mean เปิดตั้งแต่ 09.30 จนถึง 16.00 น.\n"
                f"Answer in this format: \n"
                f"EXT_SUGGEST_MONTH: <Answer>\n"
                f"EXT_SUGGEST_DAY: <Answer>\n"
                f"EXT_SUGGEST_TIME: <Answer>"
            )}
        ],
        "temperature": 0.7
    }

    try:
        response = requests.post(url, headers=headers, data=json.dumps(data))
        response.raise_for_status()  # Raise an exception for HTTP errors

        # Extract the suggestions from the response
        suggestions = {
            "EXT_SUGGEST_MONTH": "",
            "EXT_SUGGEST_DAY": "",
            "EXT_SUGGEST_TIME": ""
        }

        if response.status_code == 200:
            result = response.json()
            message_content = result['choices'][0]['message']['content'].strip()

            for line in message_content.split("\n"):
                if "EXT_SUGGEST_MONTH" in line:
                    suggestions["EXT_SUGGEST_MONTH"] = line.split(":")[1].strip()
                elif "EXT_SUGGEST_DAY" in line:
                    suggestions["EXT_SUGGEST_DAY"] = line.split(":")[1].strip()
                elif "EXT_SUGGEST_TIME" in line:
                    suggestions["EXT_SUGGEST_TIME"] = line.split(":")[1].strip()

        return suggestions

    except requests.exceptions.HTTPError as http_err:
        print(f"HTTP error occurred: {http_err}")
        return suggestions
    except Exception as e:
        print(f"Error generating suggestions with OpenAI: {e}")
        return suggestions