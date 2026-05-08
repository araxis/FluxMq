import React, {CSSProperties} from 'react';
import {
	AbsoluteFill,
	interpolate,
	spring,
	useCurrentFrame,
	useVideoConfig,
} from 'remotion';

const colors = {
	bg: '#080d12',
	panel: '#121a23',
	panel2: '#0e151d',
	border: '#263341',
	text: '#e8edf2',
	muted: '#8fa0b3',
	dim: '#516173',
	green: '#42d392',
	cyan: '#3ec7d8',
	yellow: '#f4bf75',
	red: '#ef6f6c',
	purple: '#9b87f5',
};

const topicRows = [
	['factory', '12.4k/s', colors.green, 0],
	['line-01', '8.1k/s', colors.green, 1],
	['robot-arm-07', '2.2k/s', colors.cyan, 2],
	['telemetry', '1.9k/s', colors.cyan, 3],
	['status', '286/s', colors.yellow, 3],
	['alerts', '4/s', colors.red, 3],
	['warehouse', '604/s', colors.cyan, 0],
];

const messages = [
	['20:55:18.222', 'factory/line-01/robot-arm-07/telemetry', '{"rpm":1420,"load":0.72,"temp":61.8}'],
	['20:55:18.190', 'factory/line-01/robot-arm-07/status', '{"state":"welding","cycle":44219}'],
	['20:55:18.140', 'factory/line-01/robot-arm-07/alerts', '{"severity":"warn","code":"joint-drift"}'],
	['20:55:18.108', 'factory/line-02/temperature', '{"zone":"press","value":48.1}'],
];

const payload = [
	'{',
	'  "deviceId": "robot-arm-07",',
	'  "mode": "welding",',
	'  "cycle": 44219,',
	'  "metrics": {',
	'    "rpm": 1420,',
	'    "load": 0.72,',
	'    "temperature": 61.8',
	'  }',
	'}',
];

const ease = (frame: number, input: [number, number], output: [number, number]) =>
	interpolate(frame, input, output, {extrapolateLeft: 'clamp', extrapolateRight: 'clamp'});

const panelStyle = (delay: number, frame: number): CSSProperties => {
	const y = ease(frame, [delay, delay + 24], [36, 0]);
	const opacity = ease(frame, [delay, delay + 18], [0, 1]);
	return {
		transform: `translateY(${y}px)`,
		opacity,
	};
};

const Panel: React.FC<{
	style?: CSSProperties;
	children: React.ReactNode;
	x: number;
	y: number;
	w: number;
	h: number;
}> = ({children, x, y, w, h, style}) => (
	<div
		style={{
			position: 'absolute',
			left: x,
			top: y,
			width: w,
			height: h,
			border: `1px solid ${colors.border}`,
			background: colors.panel,
			borderRadius: 8,
			boxSizing: 'border-box',
			padding: 22,
			overflow: 'hidden',
			...style,
		}}
	>
		{children}
	</div>
);

const Pill: React.FC<{children: React.ReactNode; color?: string; textColor?: string}> = ({
	children,
	color = '#1b2735',
	textColor = colors.text,
}) => (
	<span
		style={{
			display: 'inline-flex',
			alignItems: 'center',
			padding: '5px 12px',
			borderRadius: 5,
			background: color,
			color: textColor,
			fontSize: 15,
			lineHeight: 1,
		}}
	>
		{children}
	</span>
);

const Header: React.FC<{frame: number}> = ({frame}) => {
	const line = ease(frame, [0, 54], [0, 1]);
	return (
		<>
			<div
				style={{
					position: 'absolute',
					inset: '0 0 auto 0',
					height: 72,
					background: '#111821',
					borderBottom: `1px solid ${colors.border}`,
				}}
			/>
			<div style={{position: 'absolute', left: 32, top: 19, color: colors.text, fontSize: 29, fontWeight: 800}}>
				FluxMQ
				<span style={{fontSize: 15, color: colors.muted, fontWeight: 500, marginLeft: 16}}>MQTT observability workbench</span>
			</div>
			<div style={{position: 'absolute', right: 32, top: 20, display: 'flex', gap: 14}}>
				<Pill color="#112436" textColor={colors.cyan}>dev / local broker</Pill>
				<Pill color="#123326" textColor={colors.green}>Connected</Pill>
				<Pill>Replay ready</Pill>
			</div>
			<div
				style={{
					position: 'absolute',
					left: 0,
					top: 71,
					width: `${line * 100}%`,
					height: 2,
					background: `linear-gradient(90deg, ${colors.cyan}, ${colors.green}, ${colors.yellow})`,
				}}
			/>
		</>
	);
};

const SignalGraph: React.FC<{frame: number}> = ({frame}) => {
	const opacity = ease(frame, [12, 36], [0, 1]) * ease(frame, [82, 108], [1, 0.35]);
	const nodes = [
		[510, 310, 'factory'],
		[700, 235, 'line-01'],
		[908, 310, 'robot-arm-07'],
		[1132, 238, 'telemetry'],
		[1140, 390, 'alerts'],
		[1380, 315, 'FluxMQ'],
	];
	const draw = ease(frame, [18, 72], [0, 1]);
	return (
		<div style={{position: 'absolute', inset: 0, opacity}}>
			<svg width="1920" height="1080" style={{position: 'absolute'}}>
				{nodes.slice(0, -1).map((n, i) => {
					const next = nodes[i + 1];
					return (
						<line
							key={n[2] as string}
							x1={n[0] as number}
							y1={n[1] as number}
							x2={next[0] as number}
							y2={next[1] as number}
							stroke={i === 3 ? colors.red : colors.cyan}
							strokeWidth="3"
							strokeDasharray="900"
							strokeDashoffset={900 - draw * 900}
							opacity="0.8"
						/>
					);
				})}
			</svg>
			{nodes.map(([x, y, text], i) => {
				const pulse = Math.sin((frame - i * 6) / 6) * 0.5 + 0.5;
				return (
					<div key={text as string} style={{position: 'absolute', left: (x as number) - 72, top: (y as number) - 42, textAlign: 'center'}}>
						<div
							style={{
								width: i === nodes.length - 1 ? 134 : 92,
								height: i === nodes.length - 1 ? 92 : 64,
								borderRadius: 999,
								background: i === nodes.length - 1 ? '#14313a' : '#101922',
								border: `2px solid ${i === 4 ? colors.red : colors.cyan}`,
								boxShadow: `0 0 ${20 + pulse * 28}px ${i === 4 ? colors.red : colors.cyan}`,
								margin: '0 auto 12px',
							}}
						/>
						<div style={{color: colors.text, fontSize: i === nodes.length - 1 ? 22 : 16, fontWeight: 700}}>{text}</div>
					</div>
				);
			})}
		</div>
	);
};

const TopicPanel: React.FC<{frame: number}> = ({frame}) => (
	<Panel x={40} y={174} w={372} h={700} style={panelStyle(70, frame)}>
		<div style={{fontSize: 22, fontWeight: 800, marginBottom: 28}}>Topic Explorer</div>
		{topicRows.map(([name, rate, color, level], i) => (
			<div
				key={name as string}
				style={{
					display: 'grid',
					gridTemplateColumns: '1fr auto',
					alignItems: 'center',
					height: 46,
					paddingLeft: (level as number) * 20,
					opacity: ease(frame, [82 + i * 4, 100 + i * 4], [0, 1]),
					color: colors.text,
				}}
			>
				<div style={{fontSize: 16}}>{'› '.repeat(level as number)}{name}</div>
				<div style={{fontSize: 14, color: colors.muted}}>
					<span style={{display: 'inline-block', width: 9, height: 9, borderRadius: 99, background: color as string, marginRight: 8}} />
					{rate}
				</div>
			</div>
		))}
	</Panel>
);

const StreamPanel: React.FC<{frame: number}> = ({frame}) => (
	<Panel x={438} y={174} w={874} h={410} style={panelStyle(96, frame)}>
		<div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 25}}>
			<div style={{fontSize: 22, fontWeight: 800}}>Live Message Stream</div>
			<Pill color="#17314a" textColor={colors.cyan}>12.4k msg/s</Pill>
		</div>
		{messages.map(([time, topic, body], i) => (
			<div
				key={time}
				style={{
					display: 'grid',
					gridTemplateColumns: '120px 330px 1fr',
					gap: 18,
					padding: '14px 12px',
					borderRadius: 5,
					background: i === 0 ? '#192535' : i % 2 ? '#111922' : 'transparent',
					opacity: ease(frame, [106 + i * 7, 124 + i * 7], [0, 1]),
					transform: `translateX(${ease(frame, [106 + i * 7, 124 + i * 7], [36, 0])}px)`,
					fontFamily: 'Consolas, monospace',
					fontSize: 14,
				}}
			>
				<span style={{color: colors.muted}}>{time}</span>
				<span style={{color: topic.includes('alerts') ? colors.red : colors.cyan}}>{topic}</span>
				<span style={{color: colors.text}}>{body}</span>
			</div>
		))}
	</Panel>
);

const PayloadPanel: React.FC<{frame: number}> = ({frame}) => (
	<Panel x={1338} y={174} w={542} h={700} style={panelStyle(124, frame)}>
		<div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 26}}>
			<div style={{fontSize: 22, fontWeight: 800}}>Payload Inspector</div>
			<Pill color="#123326" textColor={colors.green}>Schema OK</Pill>
		</div>
		<div style={{fontSize: 15, color: colors.cyan, marginBottom: 22}}>robot-arm-07 / telemetry</div>
		<div style={{fontFamily: 'Consolas, monospace', fontSize: 18, lineHeight: 1.63}}>
			{payload.map((line, i) => (
				<div
					key={i}
					style={{
						opacity: ease(frame, [134 + i * 3, 150 + i * 3], [0, 1]),
						color: /1420|0.72|61.8/.test(line) ? colors.yellow : colors.text,
					}}
				>
					<span style={{display: 'inline-block', width: 34, color: colors.dim}}>{i + 1}</span>
					{line}
				</div>
			))}
		</div>
		<div style={{position: 'absolute', left: 22, right: 22, bottom: 28, borderTop: `1px solid ${colors.border}`, paddingTop: 22, color: colors.green, fontSize: 15}}>
			Decoded JSON · 184 bytes · validation latency 1.8 ms
		</div>
	</Panel>
);

const ReplayTimeline: React.FC<{frame: number}> = ({frame}) => {
	const progress = ease(frame, [152, 204], [0, 1]);
	const events = [
		[590, 'connect', colors.green],
		[780, 'subscribe #', colors.cyan],
		[1020, 'payload drift', colors.purple],
		[1210, 'spike', colors.red],
		[1410, 'operator publish', colors.yellow],
		[1630, 'recovered', colors.green],
	];
	return (
		<Panel x={438} y={612} w={1442} h={300} style={panelStyle(146, frame)}>
			<div style={{fontSize: 22, fontWeight: 800}}>Replay Timeline</div>
			<div style={{fontSize: 15, color: colors.muted, marginTop: 14}}>Inspect exact production history, then replay into staging with timing control.</div>
			<div style={{position: 'absolute', left: 68, right: 68, bottom: 84, height: 3, background: colors.border}}>
				<div style={{height: 3, width: `${progress * 100}%`, background: colors.cyan}} />
			</div>
			{events.map(([x, text, color], i) => {
				const visible = ease(frame, [158 + i * 6, 172 + i * 6], [0, 1]);
				return (
					<div key={text as string} style={{position: 'absolute', left: x as number, bottom: 52, opacity: visible}}>
						<div style={{position: 'absolute', left: -1, bottom: 24, width: 3, height: 74, background: color as string}} />
						<div style={{width: 22, height: 22, borderRadius: 99, background: color as string, transform: 'translate(-10px, 0)'}} />
						<div style={{position: 'absolute', width: 180, left: -54, bottom: 104, color: colors.text, fontSize: 15}}>{text}</div>
					</div>
				);
			})}
		</Panel>
	);
};

const FinalLockup: React.FC<{frame: number}> = ({frame}) => {
	const show = ease(frame, [196, 226], [0, 1]);
	const scale = spring({frame: frame - 194, fps: 30, config: {damping: 18, stiffness: 90, mass: 0.8}});
	return (
		<div
			style={{
				position: 'absolute',
				inset: 0,
				display: 'grid',
				placeItems: 'center',
				background: `rgba(8,13,18,${ease(frame, [196, 224], [0, 0.86])})`,
				opacity: show,
			}}
		>
			<div style={{textAlign: 'center', transform: `scale(${0.88 + scale * 0.12})`}}>
				<div style={{fontSize: 96, fontWeight: 900, color: colors.text, letterSpacing: 0}}>FluxMQ</div>
				<div style={{fontSize: 28, color: colors.cyan, marginTop: 16}}>Debug MQTT streams like production systems.</div>
				<div style={{display: 'flex', gap: 14, justifyContent: 'center', marginTop: 34}}>
					<Pill color="#123326" textColor={colors.green}>Observe</Pill>
					<Pill color="#17314a" textColor={colors.cyan}>Inspect</Pill>
					<Pill color="#3a2818" textColor={colors.yellow}>Replay</Pill>
				</div>
			</div>
		</div>
	);
};

export const FluxMqIntro: React.FC = () => {
	const frame = useCurrentFrame();
	const {width, height} = useVideoConfig();
	const glow = Math.sin(frame / 12) * 0.5 + 0.5;

	return (
		<AbsoluteFill style={{background: colors.bg, fontFamily: 'Segoe UI, Arial, sans-serif', color: colors.text}}>
			<div
				style={{
					position: 'absolute',
					inset: 0,
					background:
						`radial-gradient(circle at 76% 22%, rgba(62,199,216,${0.15 + glow * 0.08}), transparent 26%), ` +
						`radial-gradient(circle at 20% 70%, rgba(66,211,146,0.10), transparent 30%)`,
				}}
			/>
			<Header frame={frame} />
			<SignalGraph frame={frame} />
			<div
				style={{
					position: 'absolute',
					left: 32,
					top: 98,
					fontSize: 44,
					fontWeight: 900,
					opacity: ease(frame, [50, 76], [0, 1]),
				}}
			>
				MQTT debugging, observability, and replay
			</div>
			<div
				style={{
					position: 'absolute',
					left: 34,
					top: 154,
					fontSize: 18,
					color: colors.muted,
					opacity: ease(frame, [56, 80], [0, 1]),
				}}
			>
				From topic noise to actionable production timelines.
			</div>
			<TopicPanel frame={frame} />
			<StreamPanel frame={frame} />
			<PayloadPanel frame={frame} />
			<ReplayTimeline frame={frame} />
			<FinalLockup frame={frame} />
			<div style={{position: 'absolute', right: 26, bottom: 18, color: colors.dim, fontSize: 13}}>
				{width}x{height} · Remotion
			</div>
		</AbsoluteFill>
	);
};
