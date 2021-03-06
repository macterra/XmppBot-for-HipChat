﻿/********************************************************8
 *	Author: Andrew Deren
 *	Date: July, 2004
 *	http://www.adersoftware.com
 * 
 *	StringTokenizer class. You can use this class in any way you want
 * as long as this header remains in this file.
 * 
 **********************************************************/
using System;
using System.IO;

namespace Ader.Text
{
    /// <summary>
    /// StringTokenizer tokenized string (or stream) into tokens.
    /// </summary>
    public class StringTokenizer
    {
        const char EOF = (char)0;

        int line;
        int column;
        int pos;	// position within data

        string data;

        bool ignoreWhiteSpace;
        bool removeQuotes;
        char[] symbolChars;

        int saveLine;
        int saveCol;
        int savePos;

        public StringTokenizer(TextReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");

            data = reader.ReadToEnd();

            Reset();
        }

        public StringTokenizer(string data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            this.data = data;

            Reset();
        }

        /// <summary>
        /// gets or sets which characters are part of TokenKind.Symbol
        /// </summary>
        public char[] SymbolChars
        {
            get { return this.symbolChars; }
            set { this.symbolChars = value; }
        }

        /// <summary>
        /// if set to true, white space characters will be ignored,
        /// but EOL and whitespace inside of string will still be tokenized
        /// </summary>
        public bool IgnoreWhiteSpace
        {
            get { return this.ignoreWhiteSpace; }
            set { this.ignoreWhiteSpace = value; }
        }

        public bool RemoveQuotes
        {
            get { return this.removeQuotes; }
            set { this.removeQuotes = value; }
        }

        private void Reset()
        {
            this.ignoreWhiteSpace = false;
            this.symbolChars = new char[] { '=', '+', '-', '/', ',', '.', '*', '~', '!', '@', '#', '$', '%', '^', '&', '(', ')', '{', '}', '[', ']', ':', ';', '<', '>', '?', '|', '\\' };

            line = 1;
            column = 1;
            pos = 0;
        }

        protected char LA(int count)
        {
            if (pos + count >= data.Length)
                return EOF;
            else
                return data[pos + count];
        }

        protected char Consume()
        {
            char ret = data[pos];
            pos++;
            column++;

            return ret;
        }

        protected Token CreateToken(TokenKind kind, string value)
        {
            return new Token(kind, value, line, column);
        }

        protected Token CreateToken(TokenKind kind)
        {
            string tokenData = data.Substring(savePos, pos - savePos);
            if (this.RemoveQuotes)
            {
                if (tokenData.StartsWith("\"") && tokenData.EndsWith("\""))
                {
                    tokenData = tokenData.Substring(1, tokenData.Length - 2);
                }
            }
            return new Token(kind, tokenData, saveLine, saveCol);
        }

        public Token Next()
        {
        ReadToken:

            char ch = LA(0);
            switch (ch)
            {
                case EOF:
                    return CreateToken(TokenKind.EOF, string.Empty);

                case ' ':
                case '\t':
                    {
                        if (this.ignoreWhiteSpace)
                        {
                            Consume();
                            goto ReadToken;
                        }
                        else
                            return ReadWhitespace();
                    }
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    return ReadNumber();

                case '\r':
                    {
                        StartRead();
                        Consume();
                        if (LA(0) == '\n')
                            Consume();	// on DOS/Windows we have \r\n for new line

                        line++;
                        column = 1;

                        return CreateToken(TokenKind.EOL);
                    }
                case '\n':
                    {
                        StartRead();
                        Consume();
                        line++;
                        column = 1;

                        return CreateToken(TokenKind.EOL);
                    }

                case '"':
                    {
                        return ReadString();
                    }

                case '!':
                    {
                        return this.ReadCommand();
                    }
                default:
                    {
                        if (Char.IsLetter(ch) || ch == '_')
                            return ReadWord();
                        else if (IsSymbol(ch))
                        {
                            StartRead();
                            Consume();
                            return CreateToken(TokenKind.Symbol);
                        }
                        else
                        {
                            StartRead();
                            Consume();
                            return CreateToken(TokenKind.Unknown);
                        }
                    }

            }
        }

        /// <summary>
        /// save read point positions so that CreateToken can use those
        /// </summary>
        private void StartRead()
        {
            saveLine = line;
            saveCol = column;
            savePos = pos;
        }

        /// <summary>
        /// reads all whitespace characters (does not include newline)
        /// </summary>
        /// <returns></returns>
        protected Token ReadWhitespace()
        {
            StartRead();

            Consume(); // consume the looked-ahead whitespace char

            while (true)
            {
                char ch = LA(0);
                if (ch == '\t' || ch == ' ')
                    Consume();
                else
                    break;
            }

            return CreateToken(TokenKind.WhiteSpace);

        }

        /// <summary>
        /// reads number. Number is: DIGIT+ ("." DIGIT*)?
        /// </summary>
        /// <returns></returns>
        protected Token ReadNumber()
        {
            StartRead();

            bool hadDot = false;

            Consume(); // read first digit

            while (true)
            {
                char ch = LA(0);
                if (Char.IsDigit(ch))
                    Consume();
                else if (ch == '.' && !hadDot)
                {
                    hadDot = true;
                    Consume();
                }
                else if (ch != ' ' && ch != EOF && ch != '\t' && ch != '\r' && ch != '\n')
                {
                    return ReadWord(false);
                }
                else
                {
                    break;
                }
            }

            return CreateToken(TokenKind.Number);
        }

        /// <summary>
        /// reads word. Word contains any alpha character or _
        /// </summary>
        protected Token ReadWord(bool startRead = true)
        {
            if (startRead) { StartRead(); }

            Consume(); // consume first character of the word

            while (true)
            {
                char ch = LA(0);
                if (Char.IsLetter(ch) || ch == '_' || Char.IsNumber(ch))
                    Consume();
                else
                    break;
            }

            return CreateToken(TokenKind.Word);
        }

        /// <summary>
        /// reads all characters until next " is found.
        /// If "" (2 quotes) are found, then they are consumed as
        /// part of the string
        /// </summary>
        /// <returns></returns>
        protected Token ReadString()
        {
            StartRead();

            Consume(); // read "

            while (true)
            {
                char ch = LA(0);
                if (ch == EOF)
                    break;
                else if (ch == '\r')	// handle CR in strings
                {
                    Consume();
                    if (LA(0) == '\n')	// for DOS & windows
                        Consume();

                    line++;
                    column = 1;
                }
                else if (ch == '\n')	// new line in quoted string
                {
                    Consume();

                    line++;
                    column = 1;
                }
                else if (ch == '"')
                {
                    Consume();
                    if (LA(0) != '"')
                        break;	// done reading, and this quotes does not have escape character
                    else
                        Consume(); // consume second ", because first was just an escape
                }
                else
                    Consume();
            }

            return CreateToken(TokenKind.QuotedString);
        }

        /// <summary>
        /// checks whether c is a symbol character.
        /// </summary>
        protected bool IsSymbol(char c)
        {
            for (int i = 0; i < symbolChars.Length; i++)
                if (symbolChars[i] == c)
                    return true;

            return false;
        }

        protected Token ReadCommand()
        {

            Consume();
            // consume first character which is $. If you don't want $ to be returned
            // as part of Value of the token, just call StartRead() after Consume

            StartRead();    // this marks the position of the beginning of the token

            while (true)
            {
                char ch = LA(0);    // look at next available character
                // if it's letter or underscore, we just
                // consume it and continue reading
                if (Char.IsLetter(ch) || ch == '_')
                    Consume();
                else        // if not we break the loop
                    break;
            }

            // CreateToken creates the token with line and
            // column positions and the value of the token
            // is going to be from the string that was started
            // when StartRead was called, until current position
            return CreateToken(TokenKind.Command);
        }
    }
}
