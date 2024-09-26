import openai
from pymilvus import connections, FieldSchema, CollectionSchema, DataType, Collection, utility, db
import requests
import json
import config  # Import API key from config file
from texts import initial_texts
from pythainlp.util import normalize
from pythainlp.tokenize import word_tokenize

# Milvus connection details
MILVUS_HOST = 'localhost'
MILVUS_PORT = '19530'

# Database and collection name variables
database_name = "my_database"
collection_name = "thai_text_embeddings"

# Set the OpenAI API key from the config file
openai.api_key = config.OPENAI_API_KEY

def remove_existing_database():
    """
    Removes the existing database and all its collections.
    """
    # Connect to Milvus
    connections.connect("default", host=MILVUS_HOST, port=MILVUS_PORT)
    
    # Check if the database exists
    if database_name in db.list_database():
        print(f"Database '{database_name}' exists. Proceeding to drop all collections.")
        
        # Use the existing database
        connections.connect(host=MILVUS_HOST, port=MILVUS_PORT, db_name=database_name)
        
        # List and drop all collections
        collections = utility.list_collections()
        for collection in collections:
            utility.drop_collection(collection)
            print(f"Dropped collection: {collection}")
        
        # Drop the database
        db.drop_database(database_name)
        print(f"Database '{database_name}' has been dropped.")
    else:
        print(f"Database '{database_name}' does not exist.")
    
    # Create a new database
    db.create_database(database_name)
    print(f"Database '{database_name}' created.")

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

def create_collection(model_name):
    """
    Defines the schema for the collection and creates the collection in Milvus.
    Returns the collection.
    """
    # Determine the embedding dimension based on the model used
    if model_name == "text-embedding-3-large":
        embedding_dim = 3072  # Set dimension for large model
    else:
        embedding_dim = 1536  # Set dimension for small model (default)

    # Define the fields schema (primary key, text, and embedding vector)
    fields = [
        FieldSchema(name="id", dtype=DataType.INT64, is_primary=True, auto_id=True),  # Auto-generated ID
        FieldSchema(name="text", dtype=DataType.VARCHAR, max_length=1000),  # Store the original text
        FieldSchema(name="embedding", dtype=DataType.FLOAT_VECTOR, dim=embedding_dim)  # Adjusted embedding dimensions
    ]
    
    # Define the collection schema
    schema = CollectionSchema(fields, description=f"Collection to store Thai text and embeddings (dim={embedding_dim})")
    
    # Create collection if it does not exist
    if utility.has_collection(collection_name):
        print(f"Collection '{collection_name}' already exists.")
        collection = Collection(collection_name)
    else:
        collection = Collection(name=collection_name, schema=schema)
        print(f"Collection '{collection_name}' created with embedding dimension: {embedding_dim}.")
    
    return collection

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

def insert_texts(collection, text, embedding):
    """
    Inserts a single text and its corresponding embedding into the specified Milvus collection.
    """
    # Prepare data for insertion (text and corresponding embedding)
    data = [
        [text],        # Text field: wrapped in a list to match Milvus' insertion format
        [embedding]    # Embedding field: also wrapped in a list
    ]
    
    # Insert the data into the collection
    collection.insert(data)
    print(f"Inserted text: '{text}' into the collection.")
    
    # Ensure the data is saved
    collection.flush()


def create_index(collection):
    """
    Creates an index on the 'embedding' field for faster search.
    """
    # Check if an index already exists
    indexes = collection.indexes
    if indexes:
        print(f"An index already exists on the collection '{collection_name}'.")
    else:
        # Create an index on the "embedding" field
        index_params = {"index_type": "IVF_FLAT", "metric_type": "L2", "params": {"nlist": 128}}
        collection.create_index(field_name="embedding", index_params=index_params)
        print(f"Index created on 'embedding' field for collection '{collection_name}'.")

def search_similar_texts(collection, query_embedding, top_k=5):
    """
    Searches for texts similar to the query_embedding and returns the results in a structured format.
    """
    
    # Define the search parameters
    search_params = {"metric_type": "L2", "params": {"nprobe": 10}}
    
    # Load collection into memory for search
    collection.load()
    
    # Perform similarity search
    results = collection.search(
        [query_embedding],
        "embedding",
        param=search_params,
        limit=top_k,
        output_fields=["id", "text"]
    )
    
    # Prepare the results in a structured format to return
    json_results = []
    for result in results[0]:
        result_json = {
            "ID": result.id,
            "Text": result.entity.get("text"),  # Extract the text from the search result
            "Distance": result.distance
        }
        #print(json.dumps(result_json, indent=4, ensure_ascii=False))  # Print each JSON result with `ensure_ascii=False`
        json_results.append(result_json)
    
    # Return the results for further processing (instead of printing them)
    return json_results

def setup_collection():
    """
    Creates the collection and inserts initial data.
    """
    collection = create_collection(model_name=config.OPENAI_EMBEDDING_MODEL)
    for text in initial_texts:
        processed_text = preprocess_text(text);
        embedding = generate_embeddings_openai(text, model_name=config.OPENAI_EMBEDDING_MODEL)
        insert_texts(collection, processed_text, embedding)
    create_index(collection)
    return collection

def initialize_database():
    """
    Initializes the database by checking its existence.
    If it does not exist, it creates the database, collection, inserts texts, and creates an index.
    """
    # Connect to Milvus
    connections.connect("default", host=MILVUS_HOST, port=MILVUS_PORT)
    
    # Check if the database exists
    if database_name in db.list_database():
        print(f"Database '{database_name}' already exists. Connecting to it.")
        connections.connect(host=MILVUS_HOST, port=MILVUS_PORT, db_name=database_name)
        
        # Check if the collection exists
        if utility.has_collection(collection_name):
            print(f"Collection '{collection_name}' exists.")
            return Collection(collection_name)
        else:
            print(f"Collection '{collection_name}' does not exist. Creating collection and inserting data.")
            return setup_collection()
    else:
        print(f"Database '{database_name}' does not exist. Creating database and initializing collections.")
        remove_existing_database()
        return setup_collection()

def reset_database():
    """
    Resets the database and collection, and re-inserts the initial data.
    """
    print("Resetting the database...")
    remove_existing_database()
    collection = setup_collection()
    print("Database has been reset and new texts inserted.")
    return collection        
