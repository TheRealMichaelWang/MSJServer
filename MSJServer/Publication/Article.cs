namespace MSJServer
{
    public enum PublishStatus
    {
        Published,
        UnderReview,
        Rejected
    }

    public sealed class Article
    {
        public static bool Exists(Guid id) => File.Exists("articles/" + id.ToString());

        public static Article FromFile(Guid id)
        {
            using(FileStream stream = new FileStream("articles/" + id.ToString(), FileMode.Open, FileAccess.Read))
            using(BinaryReader reader = new BinaryReader(stream))
                return new Article(id, reader);
        }

        public static Guid[] GetPublishedArticles(DateOnly day, bool unpublished)
        {
            string[] paths = Directory.GetFiles("articles");
            List<Guid> validArticles = new List<Guid>(10);

            foreach (string path in paths)
            {
                //32 digits, 4 dashes
                Article article = FromFile(Guid.Parse(path.Substring(path.Length - 36, 36)));

                if (unpublished)
                {
                    if (article.PublishStatus == PublishStatus.Published)
                        continue;
                    if (day.DayNumber == DateOnly.FromDateTime(article.UploadTime).DayNumber)
                        validArticles.Add(article.Id);
                }
                else if (day.DayNumber == DateOnly.FromDateTime(article.PublishTime).DayNumber)
                    validArticles.Add(article.Id);
            }
            return validArticles.ToArray();
        }

        public Guid Id { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public string Snippet => Body.Substring(0, Math.Min(Body.Length, 150));

        public string Author { get; private set; }

        public DateTime PublishTime { get; private set; }
        public DateTime UploadTime { get; private set; }
        public PublishStatus PublishStatus { get; private set; }

        public Article(Guid id, string title, string body, string author, PublishStatus publishStatus, DateTime publishTime, DateTime uploadTime)
        {
            Id = id;
            Title = title;
            Body = body;
            Author = author;
            PublishStatus = publishStatus;
            PublishTime = publishTime;
            UploadTime = uploadTime;
        }

        private Article(Guid id, BinaryReader reader) : this(id, reader.ReadString(), reader.ReadString(), reader.ReadString(), (PublishStatus)reader.ReadByte(), new DateTime(reader.ReadInt64()), new DateTime(reader.ReadInt64())) { }

        private void WriteTo(BinaryWriter writer)
        {
            writer.Write(Title);
            writer.Write(Body);
            writer.Write(Author);
            writer.Write((byte)PublishStatus);
            writer.Write(PublishTime.Ticks);
            writer.Write(UploadTime.Ticks);
        }

        public void Save()
        {
            using(FileStream stream = new FileStream("articles/"+Id.ToString(), FileMode.OpenOrCreate, FileAccess.Write))
            using(BinaryWriter writer = new BinaryWriter(stream))
                WriteTo(writer);
        }

        public void Publish()
        {
            if (PublishStatus != PublishStatus.UnderReview)
                throw new InvalidOperationException();

            PublishStatus = PublishStatus.Published;
            PublishTime = DateTime.Now;
            Save();
        }

        public void Reject()
        {
            if (PublishStatus != PublishStatus.UnderReview)
                throw new InvalidOperationException();

            PublishStatus = PublishStatus.Rejected;
            Save();
        }
    }
}
