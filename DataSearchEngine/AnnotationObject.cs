using System.Collections.Generic;

namespace DataSearchEngine
{
    public class AnnotationObject
    {
        public Sentence[] sentences  { get; set; }

        public List<string> lemmas
        {
            get
            {
                var lemmaList = new List<string>(sentences.Length);
                foreach (var sentence in sentences)
                {
                    foreach (var token in sentence.tokens)
                    {
                        lemmaList.Add(token.lemma);
                    }
                }

                return lemmaList;
            }
        }
        
        public class Sentence
        {
            public int index { get; set; }
            public Token[] tokens { get; set; }

            public class Token
            {
                public int index { get; set; }
                public string word { get; set; }
                public string originalText { get; set; }
                public string lemma { get; set; }
                public int characterOffsetBegin { get; set; }
                public int characterOffsetEnd { get; set; }
                public string pos { get; set; }
                public string before { get; set; }
                public string after { get; set; }
            }
        }
    }
}