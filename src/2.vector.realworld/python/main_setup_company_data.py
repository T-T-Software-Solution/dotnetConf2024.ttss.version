import config
from helper import preprocess_text, generate_embeddings_openai, create_blank_collection, drop_and_create_vector_index

# Function to reset the collection and create vector index
def reset_collection():
    # Create a blank collection
    collection = create_blank_collection(config.MONGO_COLLECTION)
    
    # Drop and create the vector index
    drop_and_create_vector_index(collection, "embedding", "embedding_vectorSearch")

    return collection

# Main function to handle the processing
def main():
    # Initialize MongoDB collection
    collection = reset_collection()

    # Data to be processed
    initial_texts = [
        "T.T. Software Solution เป็นผู้เชี่ยวชาญด้านโซลูชันซอฟต์แวร์ที่สร้างขึ้นโดยทีมงาน MVP ที่มีความเชี่ยวชาญทางเทคนิค",
        "เราเป็นผู้นำในด้านเทคโนโลยีของ Microsoft ในประเทศไทย โดยพัฒนาโซลูชันธุรกิจด้วย ASP.NET, Azure และ C# ด้วยทีม MVP และผู้เชี่ยวชาญ",
        "หากต้องการติดต่อเรา: สำนักงานกรุงเทพฯ โทร 086-899-6243, สำนักงานขอนแก่น โทร 061-018-1275, ฝ่ายทรัพยากรบุคคล โทร 061-018-1275 หรืออีเมล hr@tt-ss.net",
        "บริการพัฒนาระบบ Back Office Web Application ที่ออกแบบและพัฒนาตามความต้องการเฉพาะขององค์กร",
        "บริการพัฒนาระบบ Data Visualization โดยออกแบบและพัฒนา Dashboard ที่ช่วยในการวิเคราะห์และตัดสินใจ",
        "บริการให้คำปรึกษา พัฒนา และดูแลระบบบน Azure Cloud โดยทีมงานมืออาชีพ เช่น การย้ายระบบไปยัง Azure Cloud, การพัฒนาโซลูชัน, การให้คำปรึกษาและอบรม รวมถึงการดูแลระบบบน Azure Cloud",
        "ผลงานภายในประเทศ: ระบบเผยแพร่ข้อมูลจัดซื้อจัดจ้างภาครัฐของกรุงเทพมหานคร บริษัทได้จัดอบรมการใช้งานระบบนี้ในวันที่ 18 กรกฎาคม 2567 และส่งงานตามกำหนดเวลา ซึ่งแสดงถึงศักยภาพของบริษัท",
        "ผลงานในต่างประเทศ: พัฒนาระบบจัดการทรัพยากรในกรณีฉุกเฉิน เช่น ซ่อมแซมโครงสร้างพื้นฐานสาธารณูปโภคและการดับไฟป่า ให้กับลูกค้าใน USA",
        "ผู้บริหารของบริษัท: CEO นคร เหรียญตระกูลชัย, CTO คุณป้องกัน, General Manager คุณวัชรพงษ์"
    ]

    # Process each text
    for text in initial_texts:
        processed_text = preprocess_text(text)
        embedding = generate_embeddings_openai(processed_text, "text-embedding-3-large")  # Use the specified model

        if embedding is not None:
            # Document structure to be inserted in MongoDB
            document = {
                "text": text,
                "processedText": processed_text,
                "embedding": embedding
            }

            # Insert the document into the collection
            collection.insert_one(document)
            print(f"Inserted document for text: {text[:30]}...")
        else:
            print(f"Failed to generate embedding for text: {text[:30]}...")

    print("All texts have been processed and inserted into the database.")

if __name__ == "__main__":
    main()
