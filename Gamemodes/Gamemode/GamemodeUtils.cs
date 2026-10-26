using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlTypes;
using UnityEngine;

public static class GamemodeUtils
{
    public static Coroutine StartCountdownTimer(int duration, Action<int> onTickCallback = null, Action onCompleteCallback = null)
    {
        return CoroutineBehaviour.Start(Countdown(duration, onTickCallback, onCompleteCallback));
    }

    private static IEnumerator Countdown(int duration, Action<int> onTickCallback, Action onCompleteCallback)
    {
        int timeRemaining = duration;
        while (timeRemaining >= 0)
        {
            onTickCallback?.Invoke(timeRemaining);

            yield return new WaitForSeconds(1.0f);

            timeRemaining--;
        }

        onCompleteCallback?.Invoke();
    }
}

public class Timer
{
    public static Dictionary<string, List<Timer>> activeTimers = new Dictionary<string, List<Timer>>();

    protected enum TimerState
    { Playing, Paused, Stopped }

    protected float duration;
    protected string key;

    protected TimerState state = TimerState.Stopped;

    protected Action<int> OnTickCallback;
    protected Action OnCompleteCallback;

    protected Coroutine timerCoroutine;

    protected Timer(float duration, string key = "default")
    {
        this.duration = duration;
        this.key = key;
    }

    public static void StopAll(string key = "default")
    {
        if (!activeTimers.ContainsKey(key)) return;

        Timer[] timers = activeTimers[key].ToArray();
        for (int i = timers.Length - 1; i >= 0; i--)
        {
            timers[i].Stop();
        }

        activeTimers[key].Clear();
    }

    public static Timer Create(float duration, string key = "default")
    {
        Timer timer = new Timer(duration, key);

        if (!activeTimers.ContainsKey(key))
        {
            activeTimers.Add(key, new List<Timer>());
        }
        activeTimers[key].Add(timer);

        return timer;
    }

    public Timer Start()
    {
        state = TimerState.Playing;

        timerCoroutine = CoroutineBehaviour.Start(Countdown(duration));

        return this;
    }

    public void Pause()
    {
        if (timerCoroutine == null) return;

        state = TimerState.Paused;
    }

    public void Resume()
    {
        if (timerCoroutine == null) return;

        state = TimerState.Playing;
    }

    public void Stop()
    {
        if (activeTimers.ContainsKey(key))
        {
            if (activeTimers[key].Contains(this))
            {
                activeTimers[key].Remove(this);
            }
        }

        if (timerCoroutine == null) return;

        state = TimerState.Stopped;

        CoroutineBehaviour.Stop(timerCoroutine);
    }


    protected IEnumerator Countdown(float duration)
    {
        int flooredDuration = Mathf.FloorToInt(duration);
        float extraTime = duration - flooredDuration;

        //use up extra time first, then start ticking.
        yield return new WaitForSeconds(extraTime);

        int timeRemaining = flooredDuration;
        while (timeRemaining > 0)
        {
            if (state == TimerState.Playing)
            {
                OnTickCallback?.Invoke(timeRemaining);

                yield return new WaitForSeconds(1.0f);

                timeRemaining--;
            }
            else
            {
                yield return null;
            }
        }

        OnTickCallback?.Invoke(timeRemaining);
        OnCompleteCallback?.Invoke();
    }

    public Timer OnTick(Action<int> callback)
    {
        OnTickCallback = callback;

        return this;
    }

    public Timer OnComplete(Action callback)
    {
        OnCompleteCallback = callback;

        return this;
    }
}

public class TransitionTimer : Timer
{
    public TransitionTimer(int duration, int transitionDuration) : base(duration)
    {

    }
}

