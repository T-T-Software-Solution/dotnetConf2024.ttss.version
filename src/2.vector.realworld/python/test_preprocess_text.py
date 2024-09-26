import re
from pythainlp.tokenize import word_tokenize
from pythainlp.util import normalize

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

# Test the function with sample input
sample_text = "ผลงานภายในประเทศ: ระบบเผยแพร่ข้อมูลจัดซื้อจัดจ้างภาครัฐของกรุงเทพมหานคร บริษัทได้จัดอบรมการใช้งานระบบนี้ในวันที่ 18 กรกฎาคม 2567 และส่งงานตามกำหนดเวลา ซึ่งแสดงถึงศักยภาพของบริษัท"
processed_text = preprocess_text(sample_text)
print(processed_text)
