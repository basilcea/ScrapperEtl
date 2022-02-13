using System;
using System.Collections.Generic;

namespace Pipeline
{
    public class Summary
    {
        public int NumberOfCategories {get;set;}
        public int NumberOfBooks {get;set;}
        public List<BooksByCategory> CategoryDetails {get; set;}
        public DateTime DateCreated {get;set;}
    }

    public class BooksByCategory {
        public string Category {get;set;}
        public int NumberOfBooks {get;set;}
        public List<Book> Books {get;set;}
    }
}