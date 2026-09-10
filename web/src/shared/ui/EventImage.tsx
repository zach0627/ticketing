import type { ImgHTMLAttributes } from 'react';

export function EventImage({
  src,
  alt,
  ...props
}: ImgHTMLAttributes<HTMLImageElement>) {
  return (
    <picture>
      <img src={src} alt={alt ?? ''} width="768" height="512" {...props} />
    </picture>
  );
}
