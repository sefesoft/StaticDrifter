using TMPro;
using UnityEngine;

namespace StaticDrift.UI
{
    /// <summary>
    /// Pulses vertex colors for one score line in a TMP block (e.g. "Top Scores" + ranked lines).
    /// </summary>
    public class GameOverTopScoreLineHighlight : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [Tooltip("0 = first score row under the header, -1 = disable.")]
        [SerializeField] private int _scoreLineIndex = -1;
        [SerializeField] private Color _highlightTint = new Color(1f, 0.88f, 0.28f, 1f);
        [SerializeField] private Color _baseTextColor = new Color(0.93f, 0.97f, 1f, 1f);
        [SerializeField] private float _pulseSpeed = 2.4f;
        [SerializeField] private float _pulseDepth = 0.22f;

        public void Configure(TMP_Text text, int scoreLineIndex)
        {
            _text = text;
            _scoreLineIndex = scoreLineIndex;
        }

        private void LateUpdate()
        {
            if (_text == null || _scoreLineIndex < 0)
            {
                return;
            }

            _text.ForceMeshUpdate();
            TMP_TextInfo info = _text.textInfo;
            if (info == null || info.lineCount == 0)
            {
                return;
            }

            int lineIdx = _scoreLineIndex + 1;
            if (lineIdx < 0 || lineIdx >= info.lineCount)
            {
                return;
            }

            float pulse = 1f - _pulseDepth * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _pulseSpeed * Mathf.PI * 2f));
            Color32 baseC = (Color32)(_baseTextColor);
            Color32 hiC = (Color32)(_highlightTint * pulse);

            for (int c = 0; c < info.characterCount; c++)
            {
                if (!info.characterInfo[c].isVisible)
                {
                    continue;
                }

                ApplyCharColor(info, c, baseC);
            }

            TMP_LineInfo line = info.lineInfo[lineIdx];
            for (int c = line.firstCharacterIndex; c <= line.lastCharacterIndex; c++)
            {
                if (c < 0 || c >= info.characterCount || !info.characterInfo[c].isVisible)
                {
                    continue;
                }

                ApplyCharColor(info, c, hiC);
            }

            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        private static void ApplyCharColor(TMP_TextInfo info, int charIndex, Color32 color)
        {
            TMP_CharacterInfo charInfo = info.characterInfo[charIndex];
            int matIndex = charInfo.materialReferenceIndex;
            int vertIndex = charInfo.vertexIndex;
            Color32[] colors = info.meshInfo[matIndex].colors32;
            if (colors == null || vertIndex + 3 >= colors.Length)
            {
                return;
            }

            colors[vertIndex] = color;
            colors[vertIndex + 1] = color;
            colors[vertIndex + 2] = color;
            colors[vertIndex + 3] = color;
        }
    }
}
