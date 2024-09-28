import 'cheerio';
import 'dotenv/config';
import { CheerioWebBaseLoader } from '@langchain/community/document_loaders/web/cheerio';
import { RecursiveCharacterTextSplitter } from 'langchain/text_splitter';
import { MemoryVectorStore } from 'langchain/vectorstores/memory';
import { OpenAIEmbeddings, ChatOpenAI } from '@langchain/openai';
import { pull } from 'langchain/hub';
import { ChatPromptTemplate } from '@langchain/core/prompts';
import { StringOutputParser } from '@langchain/core/output_parsers';
import { createStuffDocumentsChain } from 'langchain/chains/combine_documents';


// const loader = new CheerioWebBaseLoader('https://lilianweng.github.io/posts/2023-06-23-agent/');
const loader = new CheerioWebBaseLoader('https://medium.com/t-t-software-solution/rag-%E0%B8%9E%E0%B8%B7%E0%B9%89%E0%B8%99%E0%B8%90%E0%B8%B2%E0%B8%99%E0%B9%82%E0%B8%94%E0%B8%A2-semantic-kernel-c-%E0%B8%A3%E0%B9%88%E0%B8%A7%E0%B8%A1%E0%B8%81%E0%B8%B1%E0%B8%9A-chat-completion-model-%E0%B9%81%E0%B8%A5%E0%B8%B0-text-embedding-model-%E0%B9%83%E0%B8%99-azure-9a86f606c225');

const docs = await loader.load();

const textSplitter = new RecursiveCharacterTextSplitter({
  chunkSize: 1000,
  chunkOverlap: 200,
});
const splits = await textSplitter.splitDocuments(docs);
const vectorStore = await MemoryVectorStore.fromDocuments(splits, new OpenAIEmbeddings());

// Retrieve and generate using the relevant snippets of the blog.
const retriever = vectorStore.asRetriever();
const prompt = await pull<ChatPromptTemplate>('rlm/rag-prompt');
const llm = new ChatOpenAI({ model: 'gpt-4o-mini', temperature: 0 });

const ragChain = await createStuffDocumentsChain({
  llm,
  prompt,
  outputParser: new StringOutputParser(),
});

const question  = "เรียน Semantic Kernel จากไหน";

const retrievedDocs = await retriever.invoke(question);

const result = await ragChain.invoke({
    question: question,
    context: retrievedDocs,
});
console.log(result);
