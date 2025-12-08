using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Trader.Utils
{
    public interface IObservableField<out T> : IObservable<T>
    {
        T Value { get; }
        
        Task<bool> WaitFor(Predicate<T> predicate);
    }

    public class ObservableField<T> : IObservableField<T>
    {
        private readonly BehaviorSubject<T> _subject;   
        private T _value;
        private bool _completed;

        private readonly object _locker = new object();

        private List<(TaskCompletionSource<bool> tcs, Predicate<T> predicate)>? _waitingPredicates;
        
        public T Value
        {
            get
            {
                lock (_locker)
                {
                    return _value;
                }
            }
            set => SetValue(value);
        }

        public void SetValue(T value, bool checkEquality = true)
        {
            lock (_locker)
            {
                if (checkEquality && Equals(_value, value))
                {
                    return;
                }
                _value = value;
                if (_completed)
                {
                    return;
                }

                if (_waitingPredicates != null)
                {
                    for (int i = _waitingPredicates.Count - 1; i >= 0; i--)
                    {
                        if (_waitingPredicates[i].predicate(_value))
                        {
                            _waitingPredicates[i].tcs.SetResult(true);
                            _waitingPredicates.RemoveAt(i);
                        }
                    }
                }
            }
            _subject.OnNext(value);
        }

        public Task<bool> WaitFor(Predicate<T> predicate)
        {
            lock (_locker)
            {            
                if (_completed)
                {
                    return Task.FromResult(false);
                }
                
                if (predicate(_value))
                {
                    return Task.FromResult(true);
                }

                _waitingPredicates ??= new();
                
                TaskCompletionSource<bool> tcs = new();
                _waitingPredicates.Add((tcs, predicate));
                return tcs.Task;
            }
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            bool completed;
            lock (_locker)
            {
                completed = _completed;
            }

            if (completed)
            {
                observer.OnNext(_value);
                observer.OnCompleted();
                return Disposable.Empty;
            }
            
            return _subject.Subscribe(observer);
        }

        public ObservableField(T value)
        {
            _value = value;
            _subject = new BehaviorSubject<T>(value);
        }

        public void Complete()
        {
            bool complete = false;
            lock (_locker)
            {
                if (!_completed)
                {
                    _completed = true;
                    complete = true;
                    
                    if (_waitingPredicates != null)
                    {
                        foreach (var (tcs, _) in _waitingPredicates)
                        {
                            tcs.SetResult(false);
                        }
                        _waitingPredicates.Clear();
                    }
                }
            }

            if (complete)
            {
                _subject.OnCompleted();
            }
        }
        
        public static implicit operator T(ObservableField<T> field)
        {
            return field.Value;
        }

        public override string? ToString()
        {
            return Value?.ToString();
        }
    }
}