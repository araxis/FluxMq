import React, {CSSProperties} from 'react';
import {
	AbsoluteFill,
	Easing,
	Img,
	Sequence,
	interpolate,
	staticFile,
	useCurrentFrame,
	useVideoConfig,
} from 'remotion';

const colors = {
	bg: '#070b10',
	panel: '#101822',
	panel2: '#0c1219',
	panel3: '#152131',
	border: '#263443',
	text: '#edf3f7',
	muted: '#94a5b7',
	dim: '#526373',
	green: '#42d392',
	cyan: '#3ec7d8',
	yellow: '#f4bf75',
	red: '#ef6f6c',
	purple: '#9b87f5',
	blue: '#6aa7ff',
};

const fps = 30;
const totalSeconds = 120;
export const fluxMqAdDurationInFrames = totalSeconds * fps;

const voiceover = [
	{
		from: 0,
		to: 10,
		text: 'MQTT systems move fast. Most tools only let you watch the stream go by.',
	},
	{
		from: 10,
		to: 24,
		text: 'FluxMQ turns broker traffic into a working debugging, replay, and observability workspace.',
	},
	{
		from: 24,
		to: 40,
		text: 'Explore topic trees, inspect live messages, and keep high-throughput telemetry readable under pressure.',
	},
	{
		from: 40,
		to: 56,
		text: 'Decode payloads, compare messages, validate schemas, and understand what changed before it becomes an incident.',
	},
	{
		from: 56,
		to: 72,
		text: 'Record production sessions, replay them into staging, and debug timing-sensitive behavior without guessing.',
	},
	{
		from: 72,
		to: 92,
		text: 'Build real integration flows: sources, filters, dynamic mappers, validators, actors, and observers.',
	},
	{
		from: 92,
		to: 108,
		text: 'Use the same runtime for developer ELT, operations checks, metrics, assertions, and scenario testing.',
	},
	{
		from: 108,
		to: 120,
		text: 'FluxMQ. Debug MQTT streams like production systems.',
	},
];

const ease = (
	frame: number,
	input: [number, number],
	output: [number, number],
	easing: (t: number) => number = Easing.bezier(0.16, 1, 0.3, 1),
) =>
	interpolate(frame, input, output, {
		extrapolateLeft: 'clamp',
		extrapolateRight: 'clamp',
		easing,
	});

const seconds = (value: number) => value * fps;

const fadeStyle = (frame: number, inStart = 0, inEnd = 22, outStart?: number, outEnd?: number): CSSProperties => {
	const fadeIn = ease(frame, [inStart, inEnd], [0, 1]);
	const fadeOut = outStart === undefined || outEnd === undefined ? 1 : ease(frame, [outStart, outEnd], [1, 0]);
	return {opacity: fadeIn * fadeOut};
};

const card: CSSProperties = {
	border: `1px solid ${colors.border}`,
	background: colors.panel,
	borderRadius: 8,
	boxSizing: 'border-box',
	boxShadow: '0 24px 90px rgba(0,0,0,0.35)',
};

const Label: React.FC<{children: React.ReactNode; color?: string; style?: CSSProperties}> = ({
	children,
	color = colors.cyan,
	style,
}) => (
	<div
		style={{
			display: 'inline-flex',
			alignItems: 'center',
			border: `1px solid ${color}55`,
			background: `${color}16`,
			color,
			borderRadius: 5,
			padding: '8px 13px',
			fontSize: 18,
			fontWeight: 700,
			...style,
		}}
	>
		{children}
	</div>
);

const Background: React.FC<{frame: number}> = ({frame}) => {
	const drift = frame * 0.35;
	const glow = Math.sin(frame / 34) * 0.5 + 0.5;
	const packets = Array.from({length: 34}, (_, index) => {
		const x = (index * 173 + frame * (1.8 + (index % 4) * 0.6)) % 2100 - 90;
		const y = 130 + ((index * 71 + drift) % 820);
		const color = [colors.cyan, colors.green, colors.yellow, colors.purple][index % 4];
		return (
			<div
				key={index}
				style={{
					position: 'absolute',
					left: x,
					top: y,
					width: 8 + (index % 3) * 4,
					height: 2,
					background: color,
					opacity: 0.16 + (index % 5) * 0.025,
					boxShadow: `0 0 18px ${color}`,
				}}
			/>
		);
	});

	return (
		<AbsoluteFill style={{background: colors.bg}}>
			<div
				style={{
					position: 'absolute',
					inset: 0,
					background:
						`radial-gradient(circle at 76% 18%, rgba(62,199,216,${0.17 + glow * 0.07}), transparent 28%), ` +
						`radial-gradient(circle at 22% 75%, rgba(66,211,146,0.12), transparent 31%), ` +
						'linear-gradient(180deg, rgba(11,19,29,0.9), rgba(5,8,12,0.96))',
				}}
			/>
			<div
				style={{
					position: 'absolute',
					inset: 0,
					opacity: 0.16,
					backgroundImage:
						'linear-gradient(rgba(255,255,255,0.055) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.055) 1px, transparent 1px)',
					backgroundSize: '72px 72px',
					transform: `translate(${-(frame % 72)}px, ${-(frame % 72)}px)`,
				}}
			/>
			{packets}
		</AbsoluteFill>
	);
};

const BrandBar: React.FC<{frame: number}> = ({frame}) => {
	const progress = frame / fluxMqAdDurationInFrames;
	return (
		<>
			<div
				style={{
					position: 'absolute',
					left: 0,
					top: 0,
					right: 0,
					height: 74,
					background: 'rgba(10,16,23,0.9)',
					borderBottom: `1px solid ${colors.border}`,
				}}
			/>
			<div style={{position: 'absolute', left: 42, top: 19, color: colors.text, fontSize: 30, fontWeight: 900}}>
				FluxMQ
				<span style={{marginLeft: 17, color: colors.muted, fontSize: 16, fontWeight: 500}}>
					MQTT debugging and observability workbench
				</span>
			</div>
			<div style={{position: 'absolute', right: 42, top: 21, display: 'flex', gap: 12}}>
				<Label color={colors.green} style={{fontSize: 14, padding: '7px 10px'}}>
					Local-first
				</Label>
				<Label color={colors.cyan} style={{fontSize: 14, padding: '7px 10px'}}>
					Flow runtime
				</Label>
				<Label color={colors.yellow} style={{fontSize: 14, padding: '7px 10px'}}>
					Replay ready
				</Label>
			</div>
			<div style={{position: 'absolute', left: 0, right: 0, top: 73, height: 2, background: colors.border}}>
				<div
					style={{
						height: 2,
						width: `${progress * 100}%`,
						background: `linear-gradient(90deg, ${colors.cyan}, ${colors.green}, ${colors.yellow})`,
					}}
				/>
			</div>
		</>
	);
};

const CaptionTrack: React.FC<{frame: number}> = ({frame}) => {
	const currentSeconds = frame / fps;
	const caption = voiceover.find((item) => currentSeconds >= item.from && currentSeconds < item.to) ?? voiceover[voiceover.length - 1];
	const local = currentSeconds - caption.from;
	const duration = caption.to - caption.from;
	const opacity = Math.min(1, local / 0.5) * Math.min(1, (duration - local) / 0.5);

	return (
		<div
			style={{
				position: 'absolute',
				left: 360,
				right: 360,
				bottom: 34,
				minHeight: 86,
				borderRadius: 8,
				background: 'rgba(7,11,16,0.76)',
				border: `1px solid ${colors.border}`,
				padding: '18px 28px',
				boxSizing: 'border-box',
				opacity,
			}}
		>
			<div style={{fontSize: 25, lineHeight: 1.35, color: colors.text, textAlign: 'center', fontWeight: 650}}>
				{caption.text}
			</div>
			<div style={{position: 'absolute', left: 28, right: 28, bottom: 10, height: 3, background: '#1b2836'}}>
				<div
					style={{
						height: 3,
						width: `${Math.max(0, Math.min(1, local / duration)) * 100}%`,
						background: colors.cyan,
					}}
				/>
			</div>
		</div>
	);
};

const HeroProblem: React.FC = () => {
	const frame = useCurrentFrame();
	const topics = [
		'factory/line-01/robot-arm-07/telemetry',
		'factory/line-01/robot-arm-07/status',
		'factory/line-02/temperature',
		'warehouse/bay-04/scanner/events',
		'factory/line-01/robot-arm-07/alerts',
		'building/hvac/floor-03/pressure',
	];

	return (
		<AbsoluteFill style={{...fadeStyle(frame, 0, 25, 264, 300), padding: '140px 88px 0'}}>
			<div style={{display: 'grid', gridTemplateColumns: '0.9fr 1.1fr', gap: 70, alignItems: 'center', height: 820}}>
				<div>
					<Label color={colors.red}>The old MQTT workflow</Label>
					<div style={{fontSize: 84, lineHeight: 0.98, fontWeight: 950, color: colors.text, marginTop: 30}}>
						Watching is not debugging.
					</div>
					<div style={{fontSize: 28, lineHeight: 1.35, color: colors.muted, marginTop: 30, maxWidth: 720}}>
						When production traffic spikes, a passive topic viewer gives you messages. It does not give you timing,
						context, replay, or confidence.
					</div>
				</div>
				<div style={{...card, height: 650, padding: 28, background: colors.panel2, overflow: 'hidden'}}>
					<div style={{display: 'flex', justifyContent: 'space-between', marginBottom: 22}}>
						<div style={{fontSize: 24, fontWeight: 800, color: colors.text}}>Raw topic noise</div>
						<Label color={colors.red} style={{fontSize: 15, padding: '6px 10px'}}>
							12.4k msg/s
						</Label>
					</div>
					{Array.from({length: 18}, (_, index) => {
						const topic = topics[index % topics.length];
						const alert = topic.includes('alerts') || index === 10;
						const y = ((frame * 1.8 + index * 54) % 1020) - 190;
						const opacity = alert ? 1 : 0.42 + (index % 5) * 0.08;
						return (
							<div
								key={index}
								style={{
									position: 'absolute',
									left: 28,
									right: 28,
									top: 96 + y,
									height: 42,
									display: 'grid',
									gridTemplateColumns: '135px 1fr 80px',
									gap: 18,
									alignItems: 'center',
									padding: '0 14px',
									borderRadius: 5,
									background: alert ? '#301b20' : '#111b26',
									border: `1px solid ${alert ? colors.red : colors.border}55`,
									fontFamily: 'Consolas, monospace',
									fontSize: 15,
									opacity,
								}}
							>
								<span style={{color: colors.dim}}>20:55:{String(18 + index).padStart(2, '0')}.22{index % 10}</span>
								<span style={{color: alert ? colors.red : colors.cyan}}>{topic}</span>
								<span style={{color: alert ? colors.yellow : colors.muted}}>QoS {index % 2}</span>
							</div>
						);
					})}
				</div>
			</div>
		</AbsoluteFill>
	);
};

const ProductReveal: React.FC = () => {
	const frame = useCurrentFrame();
	const reveal = ease(frame, [20, 60], [0, 1]);
	const words = ['Observe', 'Inspect', 'Replay', 'Automate'];

	return (
		<AbsoluteFill style={{...fadeStyle(frame, 0, 24, 372, 420), padding: '126px 76px'}}>
			<div style={{position: 'absolute', left: 120, top: 160, right: 120, textAlign: 'center'}}>
				<div style={{fontSize: 34, color: colors.cyan, fontWeight: 800, opacity: reveal}}>Meet FluxMQ</div>
				<div style={{fontSize: 94, color: colors.text, fontWeight: 950, lineHeight: 1.02, marginTop: 20}}>
					A runtime-powered MQTT workbench.
				</div>
				<div style={{fontSize: 29, color: colors.muted, lineHeight: 1.38, margin: '34px auto 0', maxWidth: 1130}}>
					FluxMQ keeps the desktop experience dense and operational while the flow runtime handles live brokers,
					stored sessions, replay, generated traffic, and future protocols through one model.
				</div>
			</div>
			<div style={{position: 'absolute', left: 180, right: 180, bottom: 190, display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 22}}>
				{words.map((word, index) => {
					const local = frame - (92 + index * 13);
					const opacity = ease(local, [0, 18], [0, 1]);
					const y = ease(local, [0, 24], [36, 0]);
					return (
						<div
							key={word}
							style={{
								...card,
								height: 170,
								display: 'grid',
								placeItems: 'center',
								transform: `translateY(${y}px)`,
								opacity,
								background: index % 2 === 0 ? colors.panel : colors.panel3,
							}}
						>
							<div style={{fontSize: 38, color: [colors.green, colors.cyan, colors.yellow, colors.purple][index], fontWeight: 900}}>
								{word}
							</div>
						</div>
					);
				})}
			</div>
		</AbsoluteFill>
	);
};

const MockupStage: React.FC<{
	title: string;
	label: string;
	mockup: string;
	accent: string;
	points: string[];
}> = ({title, label, mockup, accent, points}) => {
	const frame = useCurrentFrame();
	const imageY = ease(frame, [12, 54], [48, 0]);
	const imageOpacity = ease(frame, [8, 40], [0, 1]);

	return (
		<AbsoluteFill style={{...fadeStyle(frame, 0, 22, 426, 480), padding: '132px 82px 0'}}>
			<div style={{display: 'grid', gridTemplateColumns: '0.88fr 1.12fr', gap: 58, alignItems: 'center'}}>
				<div>
					<Label color={accent}>{label}</Label>
					<div style={{fontSize: 70, color: colors.text, lineHeight: 1.03, fontWeight: 950, marginTop: 28}}>{title}</div>
					<div style={{display: 'grid', gap: 18, marginTop: 38}}>
						{points.map((point, index) => {
							const opacity = ease(frame, [52 + index * 18, 76 + index * 18], [0, 1]);
							return (
								<div
									key={point}
									style={{
										display: 'grid',
										gridTemplateColumns: '26px 1fr',
										gap: 16,
										alignItems: 'start',
										opacity,
										fontSize: 25,
										lineHeight: 1.32,
										color: colors.muted,
									}}
								>
									<div
										style={{
											width: 13,
											height: 13,
											borderRadius: 99,
											background: accent,
											marginTop: 10,
											boxShadow: `0 0 22px ${accent}`,
										}}
									/>
									<div>{point}</div>
								</div>
							);
						})}
					</div>
				</div>
				<div
					style={{
						...card,
						height: 670,
						padding: 16,
						background: '#0a1017',
						transform: `translateY(${imageY}px) scale(${0.96 + imageOpacity * 0.04})`,
						opacity: imageOpacity,
						overflow: 'hidden',
					}}
				>
					<Img
						src={staticFile(mockup)}
						style={{
							width: '100%',
							height: '100%',
							objectFit: 'cover',
							borderRadius: 6,
							border: `1px solid ${colors.border}`,
						}}
					/>
					<div
						style={{
							position: 'absolute',
							inset: 16,
							borderRadius: 6,
							background: `linear-gradient(110deg, transparent 0%, transparent ${30 + Math.sin(frame / 25) * 8}%, ${accent}22 48%, transparent 62%)`,
							pointerEvents: 'none',
						}}
					/>
				</div>
			</div>
		</AbsoluteFill>
	);
};

const FlowNode: React.FC<{name: string; detail: string; color: string; x: number; y: number; delay: number; frame: number}> = ({
	name,
	detail,
	color,
	x,
	y,
	delay,
	frame,
}) => {
	const opacity = ease(frame, [delay, delay + 18], [0, 1]);
	const scale = ease(frame, [delay, delay + 26], [0.9, 1]);
	return (
		<div
			style={{
				position: 'absolute',
				left: x,
				top: y,
				width: 245,
				height: 126,
				...card,
				padding: 18,
				opacity,
				transform: `scale(${scale})`,
				background: `linear-gradient(180deg, ${colors.panel3}, ${colors.panel})`,
			}}
		>
			<div style={{display: 'flex', alignItems: 'center', gap: 12}}>
				<div style={{width: 14, height: 14, borderRadius: 99, background: color, boxShadow: `0 0 20px ${color}`}} />
				<div style={{fontSize: 22, color: colors.text, fontWeight: 850}}>{name}</div>
			</div>
			<div style={{fontSize: 15, color: colors.muted, lineHeight: 1.32, marginTop: 15}}>{detail}</div>
		</div>
	);
};

const FlowScene: React.FC = () => {
	const frame = useCurrentFrame();
	const nodes = [
		{title: 'Source', detail: 'live, replay, stored, generated', color: colors.green, x: 180, y: 480},
		{title: 'Filter', detail: 'expression-backed routing', color: colors.cyan, x: 475, y: 380},
		{title: 'Mapper', detail: 'JSONata or C# expression', color: colors.purple, x: 780, y: 480},
		{title: 'Validator', detail: 'JSON Schema result stream', color: colors.yellow, x: 1085, y: 380},
		{title: 'Actor', detail: 'MQTT publisher, file writer, recorder', color: colors.red, x: 1390, y: 480},
	];

	return (
		<AbsoluteFill style={{...fadeStyle(frame, 0, 24, 546, 600), padding: '132px 84px'}}>
			<div style={{textAlign: 'center'}}>
				<Label color={colors.purple}>Developer ELT flows</Label>
				<div style={{fontSize: 62, color: colors.text, fontWeight: 950, marginTop: 22}}>Build workflows you can see.</div>
				<div style={{fontSize: 26, color: colors.muted, marginTop: 20}}>
					No hidden magic. Explicit sources, filters, mappers, validators, actors, and observers.
				</div>
			</div>
			<svg width="1920" height="1080" style={{position: 'absolute', left: 0, top: 0}}>
				{nodes.slice(0, -1).map((node, index) => {
					const next = nodes[index + 1];
					const draw = ease(frame, [106 + index * 22, 140 + index * 22], [0, 1]);
					const x1 = node.x + 245;
					const y1 = node.y + 63;
					const x2 = next.x;
					const y2 = next.y + 63;
					const mid = (x1 + x2) / 2;
					const d = `M ${x1} ${y1} C ${mid} ${y1}, ${mid} ${y2}, ${x2} ${y2}`;
					return (
						<path
							key={node.title}
							d={d}
							fill="none"
							stroke={next.color}
							strokeWidth={4}
							strokeDasharray={520}
							strokeDashoffset={520 - draw * 520}
							opacity={0.82}
						/>
					);
				})}
			</svg>
			{nodes.map((node, index) => (
				<FlowNode
					key={node.title}
					name={node.title}
					detail={node.detail}
					color={node.color}
					x={node.x}
					y={node.y}
					delay={56 + index * 18}
					frame={frame}
				/>
			))}
			<div style={{position: 'absolute', left: 270, right: 270, bottom: 170, display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 18}}>
				{['MqttEnvelope -> MqttPublishRequest', 'payloadJson -> schema result', 'record -> replay -> assert'].map((text, index) => (
					<div
						key={text}
						style={{
							...card,
							padding: '18px 20px',
							fontFamily: 'Consolas, monospace',
							fontSize: 18,
							color: [colors.green, colors.yellow, colors.cyan][index],
							opacity: ease(frame, [190 + index * 16, 214 + index * 16], [0, 1]),
						}}
					>
						{text}
					</div>
				))}
			</div>
		</AbsoluteFill>
	);
};

const OpsScene: React.FC = () => {
	const frame = useCurrentFrame();
	const metrics = [
		['Message rate', '12.4k/s', colors.cyan],
		['Schema failures', '3', colors.red],
		['Replay drift', '18 ms', colors.yellow],
		['Assertions passed', '98.7%', colors.green],
	];

	return (
		<AbsoluteFill style={{...fadeStyle(frame, 0, 22, 426, 480), padding: '132px 84px'}}>
			<div style={{display: 'grid', gridTemplateColumns: '0.92fr 1.08fr', gap: 60, alignItems: 'center'}}>
				<div>
					<Label color={colors.green}>Ops and QA era</Label>
					<div style={{fontSize: 70, color: colors.text, lineHeight: 1.04, fontWeight: 950, marginTop: 26}}>
						Turn message streams into checks.
					</div>
					<div style={{fontSize: 27, color: colors.muted, lineHeight: 1.36, marginTop: 28}}>
						Publish a request, wait for a response, validate the payload, measure the timing, and keep the evidence.
					</div>
				</div>
				<div style={{display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 22}}>
					{metrics.map(([name, value, color], index) => {
						const opacity = ease(frame, [38 + index * 18, 64 + index * 18], [0, 1]);
						return (
							<div key={name} style={{...card, height: 218, padding: 24, opacity, background: colors.panel2}}>
								<div style={{fontSize: 20, color: colors.muted}}>{name}</div>
								<div style={{fontSize: 58, color: color as string, fontWeight: 950, marginTop: 34}}>{value}</div>
								<div style={{height: 5, background: '#1a2634', marginTop: 26}}>
									<div style={{height: 5, width: `${45 + index * 13}%`, background: color as string}} />
								</div>
							</div>
						);
					})}
				</div>
			</div>
			<div style={{position: 'absolute', left: 350, right: 350, bottom: 166, ...card, padding: 26, background: '#0c151e'}}>
				<div style={{display: 'grid', gridTemplateColumns: '220px 1fr 120px', gap: 20, alignItems: 'center', fontFamily: 'Consolas, monospace', fontSize: 18}}>
					<span style={{color: colors.green}}>scenario</span>
					<span style={{color: colors.text}}>publish factory/line-01/command and expect valid status response within 2s</span>
					<span style={{color: colors.green, textAlign: 'right'}}>PASS</span>
				</div>
			</div>
		</AbsoluteFill>
	);
};

const FinalScene: React.FC = () => {
	const frame = useCurrentFrame();
	const scale = ease(frame, [30, 80], [0.92, 1]);
	const glow = Math.sin(frame / 18) * 0.5 + 0.5;
	const features = ['Explore', 'Inspect', 'Map', 'Validate', 'Replay', 'Assert'];

	return (
		<AbsoluteFill style={{...fadeStyle(frame, 0, 24), display: 'grid', placeItems: 'center'}}>
			<div
				style={{
					position: 'absolute',
					width: 720,
					height: 720,
					borderRadius: 999,
					background: `radial-gradient(circle, rgba(62,199,216,${0.18 + glow * 0.12}), transparent 67%)`,
				}}
			/>
			<div style={{textAlign: 'center', transform: `scale(${scale})`}}>
				<div style={{fontSize: 132, color: colors.text, fontWeight: 950, lineHeight: 0.92}}>FluxMQ</div>
				<div style={{fontSize: 35, color: colors.cyan, marginTop: 30, fontWeight: 800}}>
					Debug MQTT streams like production systems.
				</div>
				<div style={{display: 'flex', justifyContent: 'center', gap: 12, marginTop: 42}}>
					{features.map((feature, index) => (
						<Label key={feature} color={[colors.green, colors.cyan, colors.purple, colors.yellow, colors.blue, colors.red][index]} style={{fontSize: 17}}>
							{feature}
						</Label>
					))}
				</div>
				<div style={{fontSize: 23, color: colors.muted, marginTop: 58}}>
					Local-first desktop workbench. Runtime-driven flows. Built for MQTT, shaped for more.
				</div>
			</div>
		</AbsoluteFill>
	);
};

export const FluxMqAd: React.FC = () => {
	const frame = useCurrentFrame();
	useVideoConfig();

	return (
		<AbsoluteFill style={{fontFamily: 'Segoe UI, Arial, sans-serif', color: colors.text}}>
			<Background frame={frame} />
			<BrandBar frame={frame} />
			<Sequence from={seconds(0)} durationInFrames={seconds(10)} premountFor={seconds(1)}>
				<HeroProblem />
			</Sequence>
			<Sequence from={seconds(10)} durationInFrames={seconds(14)} premountFor={seconds(1)}>
				<ProductReveal />
			</Sequence>
			<Sequence from={seconds(24)} durationInFrames={seconds(16)} premountFor={seconds(1)}>
				<MockupStage
					label="Live workspace"
					title="Find the signal in topic traffic."
					mockup="mockups/01-main-workspace.png"
					accent={colors.cyan}
					points={[
						'Hierarchical topic explorer with activity and search.',
						'Live stream inspection without losing operational context.',
						'One workspace for live, stored, replayed, and generated data.',
					]}
				/>
			</Sequence>
			<Sequence from={seconds(40)} durationInFrames={seconds(16)} premountFor={seconds(1)}>
				<MockupStage
					label="Payload intelligence"
					title="Understand payloads before they hurt."
					mockup="mockups/02-payload-debugger.png"
					accent={colors.purple}
					points={[
						'Auto-detect JSON, text, binary, Base64, and future formats.',
						'Compare messages and spot schema drift quickly.',
						'Validate with JSON Schema and keep failures explainable.',
					]}
				/>
			</Sequence>
			<Sequence from={seconds(56)} durationInFrames={seconds(16)} premountFor={seconds(1)}>
				<MockupStage
					label="Replay and observability"
					title="Recreate production moments on demand."
					mockup="mockups/03-observability-replay.png"
					accent={colors.yellow}
					points={[
						'Record sessions with timing and message identity.',
						'Replay into staging at controlled speed.',
						'Measure rates, spikes, drops, and recovery windows.',
					]}
				/>
			</Sequence>
			<Sequence from={seconds(72)} durationInFrames={seconds(20)} premountFor={seconds(1)}>
				<FlowScene />
			</Sequence>
			<Sequence from={seconds(92)} durationInFrames={seconds(16)} premountFor={seconds(1)}>
				<OpsScene />
			</Sequence>
			<Sequence from={seconds(108)} durationInFrames={seconds(12)} premountFor={seconds(1)}>
				<FinalScene />
			</Sequence>
			<CaptionTrack frame={frame} />
		</AbsoluteFill>
	);
};
