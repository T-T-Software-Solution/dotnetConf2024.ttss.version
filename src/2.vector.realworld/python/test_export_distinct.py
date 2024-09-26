from pymongo import MongoClient
import json
import config 

client = MongoClient(config.MONGO_URI)
db = client[config.MONGO_DB]
collection = db[config.MONGO_TRAVEL_COLLECTION]

pipeline = [
    {
        "$group": {
            "_id": "$REGION_NAME_TH"
        }
    }
]

results = list(collection.aggregate(pipeline))

# Extract values from the results and join them as a comma-separated string
distinct_values = ", ".join([str(result["_id"]) for result in results if result["_id"]])

# Print the comma-separated values
print(distinct_values)
