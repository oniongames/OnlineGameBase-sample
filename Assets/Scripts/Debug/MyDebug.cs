using System.Collections;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Lightweight debug helpers that can be compiled out with build symbols.
/// </summary>
public static class MyDebug
{
    private static readonly StringBuilder Builder = new StringBuilder();

    private static bool forceIgnore;

    public static bool fSkipSystemLog;

#if !(MY_DEVELOPMENT_BUILD)
    [Conditional("MY_NEVER_DEFINED_SYMBOL")]
#endif
    public static void LogRel(string text)
    {
        if (!CanLogSystem())
            return;

        Debug.Log(Format("#### ", text));
    }

#if !(MY_DEVELOPMENT_BUILD && LOG_ENABLE )
    [Conditional("MY_NEVER_DEFINED_SYMBOL")]
#endif
    public static void Log(string text)
    {
        if (!CanLogSystem())
            return;

        Debug.Log(FormatFrame("#", text));
    }

#if !(MY_DEVELOPMENT_BUILD && LOG_ENABLE )
    [Conditional("MY_NEVER_DEFINED_SYMBOL")]
#endif
    public static void LogCond(bool condition, string text)
    {
        if (!condition || !CanLogSystem())
            return;

        Debug.Log(FormatFrame("#", text));
    }

#if !(MY_DEVELOPMENT_BUILD && LOG_ENABLE)
    [Conditional("MY_NEVER_DEFINED_SYMBOL")]
#endif
    public static void LogError(string text)
    {
        if (forceIgnore)
            return;

        Debug.LogError(FormatFrame("#### [ERROR] ", text));
    }

#if !(MY_DEVELOPMENT_BUILD && LOG_ENABLE )
    [Conditional("MY_NEVER_DEFINED_SYMBOL")]
#endif
    public static void LogSysError(string text)
    {
        if (forceIgnore)
            return;

        Debug.LogError(FormatFrame("#### [SYS ERROR] ", text));
    }

#if !(MY_DEVELOPMENT_BUILD && LOG_ENABLE )
    [Conditional("MY_NEVER_DEFINED_SYMBOL")]
#endif
    public static void LogWarning(string text)
    {
        if (!CanLogSystem())
            return;

        Debug.LogWarning(FormatFrame("#### [WARN] ", text));
    }

#if !(MY_DEVELOPMENT_BUILD && LOG_ENABLE )
    [Conditional("MY_NEVER_DEFINED_SYMBOL")]
#endif
    public static void LogIf(bool condition, string text)
    {
        if (!condition || forceIgnore)
            return;

        Debug.Log(Format("#### [LOGIF] ", text));
    }

#if !(MY_DEVELOPMENT_BUILD && LOG_ENABLE )
    [Conditional("MY_NEVER_DEFINED_SYMBOL")]
#endif
    public static void Assert(bool condition, string text)
    {
        if (condition || forceIgnore)
            return;

        Debug.LogError(FormatFrame("#### [ASSERT] ", text));
    }

#if !(MY_DEVELOPMENT_BUILD && LOG_ENABLE)
    [Conditional("MY_NEVER_DEFINED_SYMBOL")]
#endif
    public static void AssertScript(bool condition, string text)
    {
        if (condition || forceIgnore)
            return;

        Debug.LogError(FormatFrame("#### [ASSERT SCRIPT] ", text));
    }

#if !(MY_DEVELOPMENT_BUILD && LOG_ENABLE )
    [Conditional("MY_NEVER_DEFINED_SYMBOL")]
#endif
    public static void AssertWarning(bool condition, string text)
    {
        if (condition || forceIgnore)
            return;

        Debug.LogWarning(FormatFrame("#### [ASSERT WARN] ", text));
    }

#if !(MY_DEVELOPMENT_BUILD && LOG_ENABLE)
    [Conditional("MY_NEVER_DEFINED_SYMBOL")]
#endif
    public static void LogScript(string text)
    {
        if (forceIgnore)
            return;

        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", FormatFrame("-", text));
    }

#if !(MY_DEVELOPMENT_BUILD && LOG_ENABLE )
    [Conditional("MY_NEVER_DEFINED_SYMBOL")]
#endif
    public static void Dump(object value, string prefix = "", string delimiter = "\n", bool keyOnly = false)
    {
        if (forceIgnore || value == null)
            return;

        Builder.Clear();
        Builder.Append("Dumped ");
        Builder.Append(GetDumpKind(value));
        Builder.Append(":");
        Builder.Append(prefix);
        Builder.Append(":");
        Builder.Append(GetDumpCount(value));
        Builder.Append("\n");

        if (value is IDictionary dictionary)
        {
            AppendDictionaryRows(dictionary, delimiter, keyOnly);
        }
        else if (value is IEnumerable enumerable && !(value is string))
        {
            AppendEnumerableRows(enumerable, delimiter);
        }
        else
        {
            AppendDumpRow(0, value, delimiter);
        }

        Debug.Log(Builder.ToString());
    }

    private static bool CanLogSystem()
    {
        return !fSkipSystemLog && !forceIgnore;
    }

    private static string Format(string prefix, string text)
    {
        return prefix + (text ?? "NULL");
    }

    private static string FormatFrame(string prefix, string text)
    {
        return prefix + Time.frameCount.ToString("D6") + "# " + (text ?? "NULL");
    }

    private static string GetDumpKind(object value)
    {
        if (value is IDictionary)
            return "dict";

        if (value is IEnumerable && !(value is string))
            return "list";

        return "value";
    }

    private static int GetDumpCount(object value)
    {
        if (value is ICollection collection)
            return collection.Count;

        if (value is IEnumerable enumerable && !(value is string))
        {
            var count = 0;
            foreach (var _ in enumerable)
                count++;
            return count;
        }

        return 1;
    }

    private static void AppendDictionaryRows(IDictionary dictionary, string delimiter, bool keyOnly)
    {
        var i = 0;
        foreach (DictionaryEntry entry in dictionary)
        {
            Builder.AppendFormat("{0:D03}:{1}", i++, entry.Key);
            if (!keyOnly)
                Builder.AppendFormat(":{0}", entry.Value);
            Builder.Append(delimiter);
        }
    }

    private static void AppendEnumerableRows(IEnumerable enumerable, string delimiter)
    {
        var i = 0;
        foreach (var value in enumerable)
            AppendDumpRow(i++, value, delimiter);
    }

    private static void AppendDumpRow<T>(int index, T value, string delimiter)
    {
        Builder.AppendFormat("{0:D03}:{1}", index, value);
        Builder.Append(delimiter);
    }
}
