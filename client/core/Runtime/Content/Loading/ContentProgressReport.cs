using System;

namespace Internal
{
    // The bytes a fetch reports into the progress of its load. Not System.Progress: that posts to the context and would
    // show the bytes a frame late.
    internal sealed class ContentProgressReport : IProgress<long>
    {
        public ContentProgressReport(ViewableProperty<ContentProgress> progress)
        {
            _progress = progress;
        }

        private readonly ViewableProperty<ContentProgress> _progress;

        public void Report(long value)
        {
            _progress.Set(new ContentProgress(value, _progress.Value.Total));
        }
    }
}
