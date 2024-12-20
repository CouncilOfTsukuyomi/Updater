﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using ReactiveUI;
using Serilog;

namespace PenumbraModForwarder.UI.Helpers;

public class ConfigurationPropertyDescriptor : ReactiveObject
{
    public string DisplayName { get; set; }
    public string GroupName { get; set; }
    public PropertyInfo PropertyInfo { get; set; }
    public object ModelInstance { get; set; }
    public ConfigurationPropertyDescriptor ParentDescriptor { get; set; }

    private object _value;
    public object Value
    {
        get => _value;
        set
        {
            this.RaiseAndSetIfChanged(ref _value, value);
            UpdateModelValue(value);

            if (PropertyInfo.PropertyType == typeof(List<string>))
            {
                UpdatePathItems();
            }
        }
    }

    public ICommand BrowseCommand { get; set; }

    // Collection of PathItemViewModel for binding in the view
    public ObservableCollection<PathItemViewModel> PathItems { get; } = new();

    public ConfigurationPropertyDescriptor()
    {
        // Initialize commands if necessary
    }

    private void UpdatePathItems()
    {
        PathItems.Clear();
        if (Value is List<string> paths)
        {
            foreach (var path in paths)
            {
                var pathItem = new PathItemViewModel(path, this);
                PathItems.Add(pathItem);
            }
        }
    }

    internal void RemovePath(PathItemViewModel pathItem)
    {
        if (Value is List<string> paths && paths.Contains(pathItem.Path))
        {
            paths.Remove(pathItem.Path);
            PathItems.Remove(pathItem);
            this.RaisePropertyChanged(nameof(Value));
        }
    }

    private void UpdateModelValue(object value)
    {
        try
        {
            object convertedValue;
            if (PropertyInfo.PropertyType == typeof(int) && value is decimal decimalValue)
            {
                convertedValue = Convert.ToInt32(decimalValue);
            }
            else if (PropertyInfo.PropertyType == typeof(string))
            {
                convertedValue = value?.ToString();
            }
            else if (PropertyInfo.PropertyType == typeof(List<string>) && value is IEnumerable<string> enumerable)
            {
                convertedValue = new List<string>(enumerable);
            }
            else
            {
                convertedValue = Convert.ChangeType(value, PropertyInfo.PropertyType);
            }

            // Set the converted value to the model property
            PropertyInfo.SetValue(ModelInstance, convertedValue);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update model value for property {PropertyName}", PropertyInfo.Name);
        }
    }
}