using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Updater.Converts;

public class UpdateButtonTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isBlocked && isBlocked)
        {
            return "Update Blocked";
        }
        return "Update Now";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SecurityStatusCardClassConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isBlocked && isBlocked)
        {
            return "securityWarningCard";
        }
        return "infoCard";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SecurityBadgeClassConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isBlocked && isBlocked)
        {
            return "securityBlockedBadge";
        }
        return "securityOkBadge";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SecurityStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isBlocked && isBlocked)
        {
            return "🚫 UPDATE BLOCKED";
        }
        return "✅ SECURE";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SecurityWarningMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool hasWarning && hasWarning)
        {
            return "0,260,0,0";
        }
        return "0,0,0,0";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}