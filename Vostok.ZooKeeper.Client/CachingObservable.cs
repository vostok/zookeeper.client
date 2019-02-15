using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Vostok.ZooKeeper.Client
{
    internal class CachingObservable<T> : IObservable<T>
    {
        private readonly List<IObserver<T>> observers = new List<IObserver<T>>();
        private readonly object sync = new object();

        private T savedValue;
        private Exception savedError;

        public bool IsCompleted => savedError != null;

        public void Next([NotNull] T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            lock (sync)
            {
                if (IsCompleted)
                    return;

                savedValue = value;

                foreach (var observer in observers)
                    try
                    {
                        observer.OnNext(value);
                    }
                    catch
                    {
                        // ignored
                    }
            }
        }

        public void Error([NotNull] Exception error)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            lock (sync)
            {
                if (IsCompleted)
                    return;

                savedError = error;

                foreach (var observer in observers)
                    try
                    {
                        observer.OnError(error);
                    }
                    catch
                    {
                        // ignored
                    }

                observers.Clear();
            }
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            lock (sync)
            {
                if (savedError != null)
                {
                    observer.OnError(savedError);
                    return new EmptyDisposable();
                }

                if (savedValue != null)
                    observer.OnNext(savedValue);

                observers.Add(observer);
            }

            return new Subscription(this, observer);
        }

        #region Subscription

        private class Subscription : IDisposable
        {
            private readonly CachingObservable<T> observable;
            private readonly IObserver<T> observer;

            public Subscription(CachingObservable<T> observable, IObserver<T> observer)
            {
                this.observable = observable;
                this.observer = observer;
            }

            public void Dispose()
            {
                lock (observable.sync)
                {
                    observable.observers.Remove(observer);
                }
            }
        }

        #endregion

        #region EmptyDisposable

        private class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

        #endregion
    }
}