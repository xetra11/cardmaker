﻿////////////////////////////////////////////////////////////////////////////////
// The MIT License (MIT)
//
// Copyright (c) 2015 Tim Stair
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Drawing;
using CardMaker.XML;
using Support.UI;

namespace CardMaker.Events.Managers
{
    /// <summary>
    /// Handles Element related communication between components
    /// </summary>
    public class ElementManager
    {
        private static ElementManager m_zElementManager;
        private List<ProjectLayoutElement> m_listSelectedElements;

        /// <summary>
        /// Fired when an element is selected
        /// </summary>
        public event ElementSelected ElementSelected;

        /// <summary>
        /// Fired when an element is selected from the canvas (cascades into ElementSelected)
        /// </summary>
        public event ElementSelected ElementCanvasSelected;

        /// <summary>
        /// Fired when the rectangle defintion of an Element is changed (generally outside of the element control)
        /// </summary>
        public event ElementBoundsUpdated ElementBoundsUpdated;

        /// <summary>
        /// The current set of selected elements (may be empty)
        /// </summary>
        public List<ProjectLayoutElement> SelectedElements
        {
            get
            {
                // always return a copy of the list
                return m_listSelectedElements == null
                    ? m_listSelectedElements
                    : new List<ProjectLayoutElement>(m_listSelectedElements);
            }
        }

        public static ElementManager Instance
        {
            get
            {
                if (null == m_zElementManager)
                {
                    m_zElementManager = new ElementManager();
                }
                return m_zElementManager;
            }
        }

        public ElementManager()
        {
            ElementSelected += ElementManager_ElementSelected;
        }

        #region Events

        void ElementManager_ElementSelected(object sender, ElementEventArgs args)
        {
            m_listSelectedElements = new List<ProjectLayoutElement>(args.Elements);
            //Logger.AddLogLine(string.Join(",", m_listSelectedElements.Select(x => x.name).ToArray()));
        }

        #endregion

        #region Event Triggers

        public void FireElementSelectionEvent(List<ProjectLayoutElement> listElements)
        {
            if (null != ElementSelected)
            {
                ElementSelected(this, new ElementEventArgs(listElements));
            }
        }

        public void FireElementCanvasSelectedEvent(ProjectLayoutElement zElement)
        {
            if (null != ElementCanvasSelected)
            {
                ElementCanvasSelected(this, new ElementEventArgs(zElement));
            }
        }

        public void FireElementBoundsUpdateEvent()
        {
            if (null != ElementBoundsUpdated)
            {
                ElementBoundsUpdated(this, new ElementEventArgs(m_listSelectedElements));
            }
        }

        #endregion

        /// <summary>
        /// Returns the first of the selected elements
        /// </summary>
        /// <returns>The selected element or null</returns>
        public ProjectLayoutElement GetSelectedElement()
        {
            return m_listSelectedElements != null && m_listSelectedElements.Count > 0 ? m_listSelectedElements[0] : null;
        }

        /// <summary>
        /// Returns the first of the selected elements
        /// </summary>
        /// <returns>The selected element or null</returns>
        public List<ProjectLayoutElement> GetSelectedElements()
        {
            return m_listSelectedElements;
        }

        /// <summary>
        /// Adjusts the selected elements based on the passed in parameters
        /// </summary>
        /// <param name="nX">x adjustment</param>
        /// <param name="nY">y adjustment</param>
        /// <param name="nWidth">width adjustment</param>
        /// <param name="nHeight">height adjustment</param>
        /// <param name="dScaleWidth">width scale</param>
        /// <param name="dScaleHeight">height scale</param>
        public void ProcessSelectedElementsChange(int nX, int nY, int nWidth, int nHeight, decimal dScaleWidth = 1, decimal dScaleHeight = 1)
        {
            // TODO: consider breaking up the method, the input sets never overlap
            // TODO: move to a central spot (maybe a static method in ElementManager?) -- problem is the need for the selected elements
            if (null == m_listSelectedElements || m_listSelectedElements.Count == 0 ||
                (nX == 0 && nY == 0 && nWidth == 0 && nHeight == 0 && dScaleWidth == 1 && dScaleHeight == 1))
            {
                return;
            }

#warning undo/redo may be broken
            Dictionary<ProjectLayoutElement, Rectangle> dictionarySelectedUndo = GetUndoRedoPoints();

            // TODO: it is always preferred if the redo of an action is used, not the style below

            if (dScaleWidth != 1 || dScaleHeight != 1)
            {
                foreach (var zElement in m_listSelectedElements)
                {
                    zElement.width = (int)Math.Max(1, zElement.width * dScaleWidth);
                    zElement.height = (int)Math.Max(1, zElement.height * dScaleHeight);
                }
            }
            else
            {
                foreach (var zElement in m_listSelectedElements)
                {
                    zElement.x = Math.Max(1, zElement.x + nX);
                    zElement.y = Math.Max(1, zElement.y + nY);
                    zElement.width = Math.Max(1, zElement.width + nWidth);
                    zElement.height = Math.Max(1, zElement.height + nHeight);
                }
            }

            ConfigureUserAction(dictionarySelectedUndo, GetUndoRedoPoints());

            FireElementBoundsUpdateEvent();
        }

        /// <summary>
        /// Creates a collection of rectangles based on the selected list of elements
        /// </summary>
        /// <returns>The rectangle collection</returns>
        public Dictionary<ProjectLayoutElement, Rectangle> GetUndoRedoPoints()
        {
            if (null != m_listSelectedElements)
            {
                var dictionarySelectedUndo = new Dictionary<ProjectLayoutElement, Rectangle>();
                foreach (var zElement in m_listSelectedElements)
                {
                    dictionarySelectedUndo.Add(zElement, new Rectangle(zElement.x, zElement.y, zElement.width, zElement.height));
                }
                return dictionarySelectedUndo;
            }
            return null;
        }

        /// <summary>
        /// Adds a new user action based on the undo/redo collections of elements -> rectangles
        /// </summary>
        /// <param name="dictionarySelectedUndo">The undo collection of rectangles</param>
        /// <param name="dictionarySelectedRedo">The redo collection of rectangles</param>
        public void ConfigureUserAction(Dictionary<ProjectLayoutElement, Rectangle> dictionarySelectedUndo,
            Dictionary<ProjectLayoutElement, Rectangle> dictionarySelectedRedo)
        {
#warning check on closure needs for this...
            // configure the variables used for undo/redo
            var dictionaryUndoElements = dictionarySelectedUndo;
            var dictionaryRedoElements = dictionarySelectedRedo;

#warning undo/redo may be broken

            UserAction.PushAction(bRedo =>
            {
                Dictionary<ProjectLayoutElement, Rectangle> dictionaryElementsChange = bRedo
                    ? dictionaryRedoElements
                    : dictionaryUndoElements;
                foreach (var kvp in dictionaryElementsChange)
                {
                    Rectangle rectChange = kvp.Value;
                    ProjectLayoutElement zElement = kvp.Key;
                    zElement.x = rectChange.X;
                    zElement.y = rectChange.Y;
                    zElement.width = rectChange.Width;
                    zElement.height = rectChange.Height;
                }
                FireElementBoundsUpdateEvent();
            });
        }
    }
}