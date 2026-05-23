import React from 'react';
import {Composition} from 'remotion';
import {FluxMqAd, fluxMqAdDurationInFrames} from './FluxMqAd';
import {FluxMqIntro} from './FluxMqIntro';

export const Root: React.FC = () => {
	return (
		<>
			<Composition
				id="FluxMqIntro"
				component={FluxMqIntro}
				durationInFrames={240}
				fps={30}
				width={1920}
				height={1080}
			/>
			<Composition
				id="FluxMqAd"
				component={FluxMqAd}
				durationInFrames={fluxMqAdDurationInFrames}
				fps={30}
				width={1920}
				height={1080}
			/>
		</>
	);
};
