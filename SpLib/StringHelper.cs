using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpLib
{
    public static class StringHelper
    {
        public class FolderSnippet
        {
            List<string> _snippets = new List<string>();
            bool _usesRegex;
            
            public FolderSnippet(string first, bool usesRegex)
            {
                _usesRegex = usesRegex;
                _snippets.Add(first);
            }
            public void AddSnippet(string snippet)
            {
                _snippets.Add(snippet);
            }

            public string this[int index] => _snippets[index];


            public string FolderPart { get => string.Join("/", _snippets.ToArray()); } 
            public bool UsesRegex { get => _usesRegex; }
            public int Count { get => _snippets.Count(); }
        }

        public static string Unquote(string str)
        {
            return str.Trim('"', '\'');
        }

        public static FolderSnippet[] SplitIntoFolderSnippets(string name)
        {
            List<FolderSnippet> snippetList = new List<FolderSnippet>();
            var elements =  Unquote(name).Split('/');
            var snippet = WildCardToRegex(elements.First(), out bool usesRegex);
            var folderSnippet = new FolderSnippet(snippet, usesRegex);
            int i = 1;
            for (; i < elements.Length -1; i ++ )
            {
                snippet = WildCardToRegex(elements[i], out usesRegex);
                if( folderSnippet.UsesRegex || usesRegex )
                {
                    snippetList.Add(folderSnippet);
                    folderSnippet = new FolderSnippet(snippet, usesRegex);
                }
                else
                {
                    folderSnippet.AddSnippet(snippet);
                }
            }
            snippetList.Add(folderSnippet);
            // the last snippet has only one element!
            if (i < elements.Length) snippetList.Add(new FolderSnippet(WildCardToRegex(elements[i], out usesRegex), usesRegex));
            

            return snippetList.ToArray();
        }

        /// <summary>
        /// Replace all occurences of * and ? and build a valid reges pattern
        /// </summary>
        /// <param name="input"></param>
        /// <param name="usesRegex"></param>
        /// <returns></returns>
        public static string WildCardToRegex(string input, out bool usesRegex)
        {
            var expr = input;
            var pos = 0; var wildcards = "*?".ToCharArray();
            var index = expr.IndexOfAny(wildcards, pos);
            List<int> wildcardPositions = new List<int>();
            // compute the positions of the wildcards
            while (index >= 0)
            {
                wildcardPositions.Add(index);
                pos = index + 1;
                index = expr.IndexOfAny(wildcards, pos);
            }
            usesRegex = wildcardPositions.Any();
            // there are whildcards in the name!
            if (usesRegex) // form a  valid regular expression
            {
                pos = 0;
                StringBuilder sb = new StringBuilder();
                foreach (var wp in wildcardPositions)
                {
                    while (pos < wp) // copy content up to the wildcard position
                    {
                        if (expr[pos]=='.')
                        {
                            sb.Append('\\');
                        }
                        sb.Append(expr[pos++]);
                    }
                    switch (expr[pos++])
                    {
                        case '*':

                            if (wp + 1 < expr.Length)
                            {
                                var followingChar = expr[wp + 1];
                                var escape = "";
                                if (followingChar == '.')
                                {
                                    escape = "\\";
                                }
                                var stopChar = $"{escape}{followingChar}";
                                sb.Append($"([^{stopChar}]*)");
                            }
                            else sb.Append("(.*)");
                            break;
                        case '?': sb.Append('.'); break;
                        default: throw new NotSupportedException();
                    }
                }
                while (pos < expr.Length)
                {
                    if (expr[pos] == '.')
                    {
                        sb.Append('\\');
                    }
                    sb.Append(expr[pos++]);
                }
                expr = sb.ToString();
            }
            return expr;
        }
    }
}
