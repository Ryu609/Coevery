﻿using System.Collections.Generic;
using Orchard.Blogs.Models;

namespace Orchard.Blogs.ViewModels {
    public class RecentBlogPostsViewModel {
        public int Count { get; set; }
        public int BlogId { get; set; }
        public IEnumerable<BlogPart> Blogs { get; set; }

    }
}