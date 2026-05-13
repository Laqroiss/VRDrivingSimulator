'use client'
import { LineChart, Line, XAxis, YAxis, Tooltip, Legend, ResponsiveContainer, CartesianGrid } from 'recharts'

export default function SpeedChart({ track }) {
  // Даунсемплим до ~300 точек чтобы график не тормозил
  const step = Math.max(1, Math.floor(track.length / 300))
  const data = track.filter((_, i) => i % step === 0).map(pt => ({
    t:     Math.round(pt.t),
    speed: Math.round(pt.speed ?? 0),
    rpm:   Math.round((pt.rpm ?? 0) / 100) * 100,
  }))

  return (
    <ResponsiveContainer width="100%" height={220}>
      <LineChart data={data} margin={{ top: 4, right: 20, left: 0, bottom: 0 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="#2a2d3a" />
        <XAxis dataKey="t" stroke="#64748b" tick={{ fontSize: 11 }}
          tickFormatter={v => `${v}s`} />
        <YAxis yAxisId="speed" stroke="#4f8ef7" tick={{ fontSize: 11 }} domain={[0, 'auto']} />
        <YAxis yAxisId="rpm" orientation="right" stroke="#f97316" tick={{ fontSize: 11 }} domain={[0, 'auto']} />
        <Tooltip
          contentStyle={{ background: '#1a1d27', border: '1px solid #2a2d3a', borderRadius: 8 }}
          labelFormatter={v => `${v}s`}
          formatter={(val, name) => name === 'speed' ? [`${val} км/ч`, 'Скорость'] : [`${val}`, 'RPM']}
        />
        <Legend formatter={n => n === 'speed' ? 'Скорость (км/ч)' : 'RPM'} />
        <Line yAxisId="speed" type="monotone" dataKey="speed" stroke="#4f8ef7" dot={false} strokeWidth={2} />
        <Line yAxisId="rpm"   type="monotone" dataKey="rpm"   stroke="#f97316" dot={false} strokeWidth={1.5} />
      </LineChart>
    </ResponsiveContainer>
  )
}
