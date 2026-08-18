namespace GitReview.VisualStudio.Models
{
    public class DisplayOption<T>
    {
        public string Title { get; set; } = string.Empty;
        public T Value { get; set; } = default!;

        public DisplayOption(string title, T value)
        {
            Title = title;
            Value = value;
        }
    }
}
