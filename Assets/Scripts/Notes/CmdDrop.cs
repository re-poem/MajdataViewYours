using Assets.Scripts.Types;
using System;
using UnityEngine;
#nullable enable
namespace Assets.Scripts.Notes
{
    public class CmdDrop : NoteDrop
    {
        public Action Handler;

        private void Start()
        {
            timeProvider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();
        }

        protected void FixedUpdate()
        {
            var timing = GetJudgeTiming();
            if (timing >= -0.01f)
            {
                Handler();
            }
        }
    }
}
