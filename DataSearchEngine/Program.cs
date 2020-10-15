using System;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using java.util;
using java.io;
using edu.stanford.nlp.pipeline;
using Console = System.Console;
using File = System.IO.File;

namespace DataSearchEngine
{
    public static class Extensions
    {
        public static void ConsoleWriteLines(IEnumerable<string> words)
        {
            foreach (string word in words)
            {
                Console.WriteLine(word);
            }
        }
        
        public static void ConsoleWriteWords(IEnumerable<string> words)
        {
            foreach (string word in words)
            {
                Console.Write($"{word} ");
            }
        }

        //Find result by matching all words in query given with database
        public static List<int> FindAll(this Dictionary<string, HashSet<int>> invertedIndexDatabase, List<string> searchQuery)
        {
            var searchResults = new Dictionary<int, int>();

            foreach (string word in searchQuery)
            {
                if (invertedIndexDatabase.TryGetValue(word, out var documentIndexes) == false) continue;

                foreach (int documentIndex in documentIndexes)
                {
                    if (searchResults.ContainsKey(documentIndex))
                    {
                        searchResults[documentIndex]++;
                    }
                    else
                    {
                        searchResults.Add(documentIndex, 1);
                    }
                }
            }

            return (from result in searchResults where result.Value == searchQuery.Count select result.Key).ToList();
        }

        //Find result by matching any words in query given with database
        public static HashSet<string> FinAny(this Dictionary<string, List<string>> invertedIndexDatabase, IEnumerable<string> searchQuery)
        {
            var results = new HashSet<string>();

            foreach (string word in searchQuery)
            {
                if (invertedIndexDatabase.TryGetValue(word, out var documents))
                {
                    results.UnionWith(documents);
                }
            }

            return results;
        }
    }

    internal class EntryPoint
    {
        public static StanfordCoreNLP Pipeline;
        public static Dictionary<string, HashSet<int>> InvertedIndexDatabase;

        //For debugging
        public static List<string> DocumentNames;

        [STAThread]
        private static void Main(string[] args)
        {
            //-------------------------------------------------------------------------
            
            string modelsPath = @"C:\Users\Eimantas\Documents\Projects\Data Search AI\DataSearchEngine\stanford-corenlp-3.7.0-models";
            if (args.Length > 0)
                modelsPath = args[0];

            Console.WriteLine($"Initializing {nameof(StanfordCoreNLP)}...\n");
            
            // Annotation pipeline configuration
            var props = new Properties();
            props.setProperty("annotators", "tokenize, ssplit, pos, lemma,");
            props.setProperty("ner.useSUTime", "0");

            // We should change current directory, so StanfordCoreNLP could find all the model files automatically
            string curDir = Environment.CurrentDirectory;
            Directory.SetCurrentDirectory(modelsPath);
            Pipeline = new StanfordCoreNLP(props);
            Directory.SetCurrentDirectory(curDir);


            //Get documents directory
            string documentPath = @"C:\Users\Eimantas\Documents\Projects\Data Search AI\DataSearchEngine\DataToSearchFor";
            if (args.Length > 1)
                documentPath = args[1];
            
            var    files        = Directory.GetFiles(documentPath);

            //Initialize database
            InvertedIndexDatabase = new Dictionary<string, HashSet<int>>();
            DocumentNames         = new List<string>(files.Length);

            Console.WriteLine("\nBuilding Database...\n");
            
            //Populate database
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                DocumentNames.Add(Path.GetFileName(file));

                string content = File.ReadAllText(file);

                var annotation = new Annotation(content);
                Pipeline.annotate(annotation);
                
                using var stream = new ByteArrayOutputStream();
                Pipeline.jsonPrint(annotation, new PrintWriter(stream));
                string serialized   = stream.toString().Replace("\n", "");
                var    deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<AnnotationSerializer>(serialized);
                AssignIndex(deserialized.lemmas, i);
                stream.close();
            }
            
            Console.WriteLine("\n---Database Built Successfully---\n");
            
            //Console Input --- search query
            Console.WriteLine("Input Your Search Query");
            string query = Console.ReadLine();
            if (query == null)
            {
                Console.WriteLine("Search query is empty, no results will be shown");
                Console.ReadLine();
                return;
            }
            
            // Annotation
            var queryAnnotation = new Annotation(query);
            Pipeline.annotate(queryAnnotation);

            // Result - Print
            using (var stream = new ByteArrayOutputStream())
            {
                Pipeline.jsonPrint(queryAnnotation, new PrintWriter(stream));

                //-----
                string serialized   = stream.toString().Replace("\n", "");
                var    deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<AnnotationSerializer>(serialized);
                //-----

                //Find results by "All" condition
                var indexedResults = InvertedIndexDatabase.FindAll(FilterContent(deserialized.lemmas));
                Console.Write($"\n--->Your input search query after lemmatization:\t");
                Extensions.ConsoleWriteWords(deserialized.lemmas);
                Console.WriteLine();

                //Map document indexes back to readable names
                var results = new List<string>(indexedResults.Count);
                results.AddRange(indexedResults.Select(result => DocumentNames[result]));

                //Console Output --- search results
                if (results.Count == 0)
                {
                    Console.WriteLine("\nNo Results Found");
                }
                else
                {
                    Console.WriteLine("\nResults:");
                    Console.WriteLine();
                    Extensions.ConsoleWriteLines(results);
                    Console.WriteLine();
                }
                
                //-----
                stream.close();
            }
        }

        //Create or assign indexes for words and documents
        private static void AssignIndex(List<string> content, int document)
        {
            var words = FilterContent(content);
            foreach (string word in words)
            {
                if (InvertedIndexDatabase.ContainsKey(word) == false)
                {
                    InvertedIndexDatabase.Add(word, new HashSet<int> {document});
                }
                else
                {
                    InvertedIndexDatabase[word].Add(document);
                }
            }
        }

        public static List<string> FilterContent(List<string> words)
        {
            var filteredContent = new List<string>(words.Count);
            foreach (string word in words)
            {
                string filteredWord = word.ToLower();
                filteredContent.Add(filteredWord);
            }

            return filteredContent;
        }
    }
}