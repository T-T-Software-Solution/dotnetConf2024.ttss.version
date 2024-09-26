import config
import pandas as pd
from helper import create_blank_collection, generate_suggestions_openai, preprocess_text, generate_embeddings_openai, drop_and_create_vector_index

# Define all Thai months and days
THAI_MONTHS = "มกราคม, กุมภาพันธ์, มีนาคม, เมษายน, พฤษภาคม, มิถุนายน, กรกฎาคม, สิงหาคม, กันยายน, ตุลาคม, พฤศจิกายน, ธันวาคม, ทั้งปี"
THAI_DAYS = "จันทร์, อังคาร, พุธ, พฤหัสบดี, ศุกร์, เสาร์, อาทิตย์, ทุกวัน"
THAI_TIMES = "เช้า, บ่าย, เย็น, ค่ำ, กลางคืน, ตลอดเวลา, ทั้งวัน"

# Function to reset the collection and create vector index
def reset_collection():
    # Create a blank collection
    collection = create_blank_collection(config.MONGO_TRAVEL_COLLECTION)
    return collection

# Function to safely get text and replace unwanted values
def clean_text(text):
    if pd.isna(text) or text in ["nan", None]:
        return ""
    return str(text).replace("_x000D_", "").replace("nan", "").strip()

# Function to read Excel data and insert it into MongoDB
def read_and_insert_excel_data(collection, file_path, num_rows=None):
    try:
        # Read Excel file into a DataFrame
        df = pd.read_excel(file_path)

        # Select only the first `num_rows` rows if specified, else all rows
        if num_rows is not None:
            df = df.head(num_rows)

        # Convert DataFrame to a list of dictionaries
        data_list = df.to_dict(orient='records')

        # Get the total number of rows
        total_rows = len(data_list)
        batch_data = []  # List to hold batch data

        for idx, data in enumerate(data_list, start=1):
            print(f"Processing row {idx} of {total_rows}...")  # Show current row and total number of rows

            att_start_end = data.get("ATT_START_END", "")
            if att_start_end:
                # Generate suggestions using OpenAI
                suggestions = generate_suggestions_openai(att_start_end)

                # Check conditions and update suggestions accordingly
                if suggestions.get("EXT_SUGGEST_MONTH") in ["ทั้งปี", "nan", None, ""]:
                    suggestions["EXT_SUGGEST_MONTH"] = THAI_MONTHS

                if suggestions.get("EXT_SUGGEST_DAY") in ["ทุกวัน", "nan", None, ""]:
                    suggestions["EXT_SUGGEST_DAY"] = THAI_DAYS

                if suggestions.get("EXT_SUGGEST_TIME") in ["ตลอดเวลา", "nan", None, ""]:
                    suggestions["EXT_SUGGEST_TIME"] = THAI_TIMES

                # Update the data dictionary with the processed suggestions
                data.update(suggestions)

                # Clean ATT_DETAIL_TH before creating EXT_TRAVEL_DETAIL
                data['ATT_DETAIL_TH'] = clean_text(data.get('ATT_DETAIL_TH', ''))

                # Create EXT_TRAVEL_DETAIL field with combined information and clean unwanted values
                ext_travel_detail = (
                    f"ชื่อ: {clean_text(data.get('ATT_NAME_TH', ''))} {clean_text(data.get('ATT_NAME_EN', ''))}\n"
                    f"ที่ตั้ง: {clean_text(data.get('REGION_NAME_TH', ''))} {clean_text(data.get('PROVINCE_NAME_TH', ''))} "
                    f"{clean_text(data.get('DISTRICT_NAME_TH', ''))} {clean_text(data.get('SUBDISTRICT_NAME_TH', ''))}\n"
                    f"หมวดหมู่: {clean_text(data.get('ATTR_CATAGORY_TH', ''))} {clean_text(data.get('ATTR_SUB_TYPE_TH', ''))}\n"
                    f"เวลาบริการ: {clean_text(suggestions['EXT_SUGGEST_MONTH'])} {clean_text(suggestions['EXT_SUGGEST_DAY'])} {clean_text(suggestions['EXT_SUGGEST_TIME'])}\n"
                    f"รายละเอียด: {clean_text(data.get('ATT_DETAIL_TH', ''))}"
                )
                
                data['EXT_TRAVEL_DETAIL'] = ext_travel_detail

                # Process EXT_TRAVEL_DETAIL with preprocess_text and generate embeddings
                processed_text = preprocess_text(ext_travel_detail)
                data['processedText'] = processed_text

                # Generate embeddings using OpenAI with processedText as input
                embeddings = generate_embeddings_openai(processed_text, "text-embedding-3-large")  # Use the desired embedding model
                data['embedding'] = embeddings

            # Append processed data to the batch list
            batch_data.append(data)

            # Insert the batch into MongoDB after every 10 items
            if len(batch_data) == 10:
                collection.insert_many(batch_data)
                print(f"Inserted {len(batch_data)} records into the collection '{collection.name}'.")
                batch_data = []  # Clear the batch list

        # Insert any remaining data if not already inserted
        if batch_data:
            collection.insert_many(batch_data)
            print(f"Inserted {len(batch_data)} remaining records into the collection '{collection.name}'.")
    except Exception as e:
        print(f"Error reading and inserting data from Excel: {e}")

# Main function to handle the processing
def main():
    # Initialize MongoDB collection
    collection = reset_collection()

    # Define the path to the Excel file
    file_path = "82f307c8-490e-432c-a613-be7bf841860a.xlsx"  # Replace with your actual file path

    # Optionally, create vector index if needed (if using vector-based queries)
    drop_and_create_vector_index(collection, index_field="embedding", index_name="travel_vector_index")

    # Read data from Excel and insert into MongoDB collection
    read_and_insert_excel_data(collection, file_path, num_rows=None)

if __name__ == "__main__":
    main()
