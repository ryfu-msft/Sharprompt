﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Sharprompt.Internal;

namespace Sharprompt.Forms
{
    internal class ListForm<T> : FormBase<IEnumerable<T>>
    {
        public ListForm(ListOptions<T> options)
        {
            if (options.Minimum < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.Minimum), $"The minimum ({options.Minimum}) is not valid");
            }

            if (options.Maximum < options.Minimum)
            {
                throw new ArgumentException($"The maximum ({options.Maximum}) is not valid when minimum is set to ({options.Minimum})", nameof(options.Maximum));
            }

            _options = options;

            _inputItems.AddRange(options.DefaultValues ?? Enumerable.Empty<T>());
        }

        private readonly ListOptions<T> _options;

        private readonly Type _targetType = typeof(T);
        private readonly Type _underlyingType = Nullable.GetUnderlyingType(typeof(T));

        private int _startIndex;
        private readonly StringBuilder _inputBuffer = new();
        private readonly List<T> _inputItems = new();

        protected override bool TryGetResult(out IEnumerable<T> result)
        {
            do
            {
                var keyInfo = ConsoleDriver.ReadKey();

                switch (keyInfo.Key)
                {
                    case ConsoleKey.Enter:
                    {
                        var input = _inputBuffer.ToString();

                        try
                        {
                            result = _inputItems;

                            if (string.IsNullOrEmpty(input))
                            {
                                if (_inputItems.Count >= _options.Minimum)
                                {
                                    return true;
                                }

                                SetError($"A minimum input of {_options.Minimum} items is required");

                                return false;
                            }

                            if (_inputItems.Count >= _options.Maximum)
                            {
                                SetError($"A maximum input of {_options.Maximum} items is required");

                                return false;
                            }

                            var inputValue = (T)Convert.ChangeType(input, _underlyingType ?? _targetType);

                            if (!TryValidate(inputValue, _options.Validators))
                            {
                                return false;
                            }

                            _startIndex = 0;
                            _inputBuffer.Clear();

                            _inputItems.Add(inputValue);

                            return false;
                        }
                        catch (Exception ex)
                        {
                            SetError(ex);
                        }

                        break;
                    }
                    case ConsoleKey.LeftArrow when _startIndex > 0:
                        _startIndex -= 1;
                        break;
                    case ConsoleKey.RightArrow when _startIndex < _inputBuffer.Length:
                        _startIndex += 1;
                        break;
                    case ConsoleKey.Backspace when _startIndex > 0:
                        _inputBuffer.Remove(--_startIndex, 1);
                        break;
                    case ConsoleKey.Delete when _startIndex < _inputBuffer.Length:
                        _inputBuffer.Remove(_startIndex, 1);
                        break;
                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.RightArrow:
                    case ConsoleKey.Backspace:
                    case ConsoleKey.Delete:
                        ConsoleDriver.Beep();
                        break;
                    default:
                        if (!char.IsControl(keyInfo.KeyChar))
                        {
                            _inputBuffer.Insert(_startIndex++, keyInfo.KeyChar);
                        }
                        break;
                }

            } while (ConsoleDriver.KeyAvailable);

            result = null;

            return false;
        }

        protected override void InputTemplate(OffscreenBuffer offscreenBuffer)
        {
            offscreenBuffer.WritePrompt(_options.Message);

            offscreenBuffer.Write(_inputBuffer.ToString(0, _startIndex));

            offscreenBuffer.PushCursor();

            offscreenBuffer.Write(_inputBuffer.ToString(_startIndex, _inputBuffer.Length - _startIndex));

            foreach (var inputItem in _inputItems)
            {
                offscreenBuffer.WriteLine();
                offscreenBuffer.Write($"  {inputItem}");
            }
        }

        protected override void FinishTemplate(OffscreenBuffer offscreenBuffer, IEnumerable<T> result)
        {
            offscreenBuffer.WriteFinish(_options.Message);
            offscreenBuffer.Write(string.Join(", ", result), Prompt.ColorSchema.Answer);
        }
    }
}
