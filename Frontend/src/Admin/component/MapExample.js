import { GoogleMap, LoadScript, Marker } from '@react-google-maps/api';

export default function MapExample() {
    const defaultCenter = {
        lat: 40.748817,
        lng: -73.985428,
    };

    return (
        <div className="admin-card admin-map-card relative w-full">
            <LoadScript googleMapsApiKey="AIzaSyCN5RsuQUGXEAd3TqNpEkHygtmhFxNiDZk">
                <GoogleMap
                    mapContainerClassName="admin-map"
                    zoom={13}
                    center={defaultCenter}
                >
                    <Marker key="location" position={defaultCenter} />
                </GoogleMap>
            </LoadScript>
        </div>
    );
}
